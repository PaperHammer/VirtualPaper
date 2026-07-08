using System.IO;
using System.IO.Compression;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Cores.AppUpdate.Models;
using VirtualPaper.lang;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Views;
using Windows.AI.MachineLearning;

namespace VirtualPaper.Cores.AppUpdate.Specific {
    public interface IPluginsUpdateService {
        Task<PluginsUpdateResult> DownloadPendingAsync(ReleaseInfo releaseInfo, IProgress<DownloadProgress>? progress = null, CancellationToken token = default);
        Task<PluginsUpdateResult> VerifyAndSavePendingAsync(ReleaseInfo releaseInfo, CancellationToken token = default);
        Task<PluginsUpdateResult> ExecutePendingUpdateAsync(IProgress<PluginsUpdateProgress>? progress = null, CancellationToken token = default);
        Task<bool> CheckAndRecoverAsync(CancellationToken token = default);

        /// <summary>
        /// 执行待处理的插件更新，显示进度窗口
        /// </summary>
        Task ExecutePendingPluginUpdateWithWindowAsync();
    }

    public interface IPluginsUpdateServiceInit {
        Task InitAsync();
    }

    public class PluginsUpdateService(
        IDownloadService downloadService,
        IJobService jobService,
        IAppBuildService appBuildService,
        IWindowService windowService) : IPluginsUpdateService, IPluginsUpdateServiceInit {
        // ===== TEST HOOKS - 模拟测试用，后期删除 =====
        public static bool ForceRollback { get; set; } = false;
        // ==========================================

        public async Task InitAsync() {
            UpdateLock.RegisterAll();

            var hasPending = await CheckAndRecoverAsync();
            if (hasPending) {
                await ExecutePendingPluginUpdateWithWindowAsync();
            }
            else {
                UpdateLock.ReleaseAll();
            }
            appBuildService.Refresh();
        }

        public async Task<PluginsUpdateResult> DownloadPendingAsync(ReleaseInfo releaseInfo, IProgress<DownloadProgress>? progress = null, CancellationToken token = default) {
            var result = new PluginsUpdateResult();

            if (releaseInfo.PluginPatchUri == null) {
                result.Success = false;
                result.ErrorMessage = "No plugin patch available";
                return result;
            }

            var pendingDir = Constants.CommonPaths.PendingPluginsUpdateDir;
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            try {
                Directory.CreateDirectory(pendingDir);

                var patchZipPath = Path.Combine(pendingDir, "plugins_patch.zip");
                await foreach (var p in downloadService.DownloadAsync(releaseInfo.PluginPatchUri, patchZipPath, linkedCts.Token)) {
                    progress?.Report(p);
                }

                result.Success = true;
            }
            catch (Exception ex) when (!token.IsCancellationRequested) {
                ArcLog.GetLogger<PluginsUpdateService>().Error("Plugin patch download failed", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                // Keep downloaded files for potential resume
            }
            finally {
                linkedCts.Cancel();
            }

            return result;
        }

        public async Task<PluginsUpdateResult> VerifyAndSavePendingAsync(ReleaseInfo releaseInfo, CancellationToken token = default) {
            var result = new PluginsUpdateResult();

            if (releaseInfo.PluginPatchSha256Uri == null) {
                result.Success = false;
                result.ErrorMessage = "No plugin patch SHA256 available";
                return result;
            }

            var pendingDir = Constants.CommonPaths.PendingPluginsUpdateDir;
            var patchZipPath = Path.Combine(pendingDir, "plugins_patch.zip");

            try {
                // Download and verify SHA256 of plugins_patch.zip
                var expectedHash = await downloadService.DownloadShaTxtAsync(releaseInfo.PluginPatchSha256Uri, token);
                bool verified = await downloadService.VerifyFileIntegrityAsync(patchZipPath, expectedHash, token);
                if (!verified) {
                    throw new InvalidDataException("SHA256 verification failed for plugins_patch.zip");
                }

                // Extract plugins_patch.zip
                var extractDir = Constants.CommonPaths.PluginPatchExtractDir;
                if (Directory.Exists(extractDir)) {
                    Directory.Delete(extractDir, true);
                }
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(patchZipPath, extractDir, true);

                // Parse pending_update_plugins_manifest.json
                var pendingManifestPath = Path.Combine(extractDir, "pending_update_plugins_manifest.json");
                if (!File.Exists(pendingManifestPath)) {
                    throw new FileNotFoundException("pending_update_plugins_manifest.json not found in patch");
                }
                var pendingJson = await File.ReadAllTextAsync(pendingManifestPath, token);
                var pendingManifest = JsonSerializer.Deserialize(pendingJson, UpdateManifestContext.Default.PendingUpdateManifest);
                if (pendingManifest == null || pendingManifest.Plugins.Count == 0) {
                    throw new InvalidOperationException("No plugins to update in pending manifest");
                }

                // Parse app_comp_manifest.json
                var appCompManifestPath = Path.Combine(extractDir, "app_comp_manifest.json");
                AppCompManifest? appCompManifest = null;
                if (File.Exists(appCompManifestPath)) {
                    var appCompJson = await File.ReadAllTextAsync(appCompManifestPath, token);
                    appCompManifest = JsonSerializer.Deserialize(appCompJson, UpdateManifestContext.Default.AppCompManifest);
                }

                // Build update flag
                var updateFlag = new PluginsUpdateFlag {
                    Status = UpdateStatus.Pending,
                    AppBuildNumber = appCompManifest?.AppBuildNumber ?? string.Empty,
                    Plugins = new Dictionary<string, PluginFlagInfo>()
                };

                foreach (var (pluginName, pluginInfo) in pendingManifest.Plugins) {
                    // Verify individual plugin zip exists in extracted content
                    var pluginZipPath = Path.Combine(extractDir, pluginInfo.Asset);
                    if (!File.Exists(pluginZipPath)) {
                        throw new FileNotFoundException($"Plugin zip not found in patch: {pluginInfo.Asset}");
                    }

                    // Cross-verify: compute actual SHA256 of plugin zip and compare with manifest declaration.
                    // This catches tampering with the manifest's sha256 field after the outer zip was extracted.
                    bool pluginZipVerified = await downloadService.VerifyFileIntegrityAsync(pluginZipPath, pluginInfo.Sha256, token);
                    if (!pluginZipVerified) {
                        throw new InvalidDataException($"SHA256 verification failed for plugin zip: {pluginInfo.Asset}");
                    }

                    updateFlag.Plugins[pluginName] = new PluginFlagInfo {
                        Target = Path.Combine("Plugins", pluginName),
                        Build = pluginInfo.BuildNumber,
                        Files = new List<FileHashInfo> {
                            new FileHashInfo {
                                Name = pluginInfo.Asset,
                                Sha256 = pluginInfo.Sha256
                            }
                        }
                    };
                }

                await SaveUpdateFlagAsync(updateFlag, token);

                // Store release info for later use
                releaseInfo.AppCompManifest = appCompManifest;
                releaseInfo.AppBuild = appCompManifest?.AppBuildNumber;

                result.Success = true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PluginsUpdateService>().Error("Plugin patch verify failed", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                FileUtil.RemoveDirectory(pendingDir);
            }

            return result;
        }

        /// <summary>
        /// Execute a pending update: close UI, backup, replace, cleanup.
        /// Can be called immediately after download, or later on UI close / core start.
        /// </summary>
        public async Task<PluginsUpdateResult> ExecutePendingUpdateAsync(IProgress<PluginsUpdateProgress>? progress = null, CancellationToken token = default) {
            var result = new PluginsUpdateResult();
            var pendingDir = Constants.CommonPaths.PendingPluginsUpdateDir;
            var flagPath = Constants.CommonPaths.UpdateFlagPath;

            if (!File.Exists(flagPath)) {
                result.Success = false;
                result.ErrorMessage = "No pending update found";
                return result;
            }

            var flag = await LoadUpdateFlagAsync(token);
            if (flag == null || flag.Status != UpdateStatus.Pending) {
                result.Success = false;
                result.ErrorMessage = "No pending update found or invalid state";
                return result;
            }

            try {
                var extractDir = Constants.CommonPaths.PluginPatchExtractDir;

                // Verify extracted plugin zips against hashes in flag
                foreach (var (pluginName, pluginInfo) in flag.Plugins) {
                    foreach (var fileHash in pluginInfo.Files) {
                        var filePath = Path.Combine(extractDir, fileHash.Name);
                        if (!File.Exists(filePath)) {
                            throw new FileNotFoundException($"File missing: {Path.GetFileName(filePath)}");
                        }
                        bool verified = await downloadService.VerifyFileIntegrityAsync(filePath, fileHash.Sha256, token);
                        if (!verified) {
                            throw new InvalidDataException($"File verification failed: {Path.GetFileName(filePath)}");
                        }
                    }
                }

                // Lock first (prevent restart), then stop processes
                var updatingPlugins = ParsePluginNames(flag.Plugins.Keys);
                UpdateLock.LockAll(updatingPlugins);
                StopPlugins(updatingPlugins);

                // Step: Backup current plugins
                progress?.Report(new PluginsUpdateProgress(PluginsUpdateStage.BackingUp, 0, LanguageManager.Instance[nameof(Constants.I18n.PluginsUpdate_Stage_BackingUp)]));
                var backupDir = Constants.CommonPaths.UpdateBackupDir;
                FileUtil.RemoveDirectory(backupDir);
                Directory.CreateDirectory(backupDir);

                // ===== TEST HOOK - 模拟回滚 =====
                if (ForceRollback) {
                    throw new Exception("[TEST] Forced rollback during update");
                }
                // ================================

                foreach (var (pluginName, pluginInfo) in flag.Plugins) {
                    var sourceDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginInfo.Target));
                    if (Directory.Exists(sourceDir)) {
                        var backupPath = Path.Combine(backupDir, pluginName);
                        FileUtil.CopyDirectory(sourceDir, backupPath, true);
                    }
                }

                // Backup app_comp_manifest.json from WorkSpace root
                var appCompManifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_comp_manifest.json");
                if (File.Exists(appCompManifestPath)) {
                    var appManifestBackup = Path.Combine(backupDir, "app_comp_manifest.json");
                    File.Copy(appCompManifestPath, appManifestBackup, true);
                }

                // Step: Update flag to in_progress
                flag.Status = UpdateStatus.InProgress;
                await SaveUpdateFlagAsync(flag, token);

                // Step: Replace plugins sequentially (avoid IO contention on Windows)
                progress?.Report(new PluginsUpdateProgress(PluginsUpdateStage.Replacing, 0, LanguageManager.Instance[nameof(Constants.I18n.PluginsUpdate_Stage_Replacing)]));
                int totalPlugins = flag.Plugins.Count;
                int replacedCount = 0;

                foreach (var (pluginName, pluginInfo) in flag.Plugins) {
                    token.ThrowIfCancellationRequested();

                    var targetDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginInfo.Target));
                    var zipPath = Path.Combine(extractDir, pluginInfo.Files[0].Name);

                    // Clear target directory
                    if (Directory.Exists(targetDir)) {
                        FileUtil.DeleteDirectoryContents(targetDir);
                    }
                    else {
                        Directory.CreateDirectory(targetDir);
                    }

                    // Extract plugin zip - the zip contains a single folder with the plugin name
                    var pluginExtractDir = Path.Combine(pendingDir, pluginName, "extracted");
                    if (Directory.Exists(pluginExtractDir)) {
                        Directory.Delete(pluginExtractDir, true);
                    }
                    Directory.CreateDirectory(pluginExtractDir);
                    ZipFile.ExtractToDirectory(zipPath, pluginExtractDir, true);

                    // Find the plugin folder inside the extracted content
                    var folders = Directory.GetDirectories(pluginExtractDir);
                    if (folders.Length != 1) {
                        throw new InvalidOperationException($"Expected exactly one folder in plugin zip, found {folders.Length}");
                    }
                    var pluginFolder = folders[0];

                    // Move contents from plugin folder to target
                    foreach (var item in Directory.GetFileSystemEntries(pluginFolder)) {
                        var destPath = Path.Combine(targetDir, Path.GetFileName(item));
                        if (Directory.Exists(item)) {
                            FileUtil.CopyDirectory(item, destPath, true);
                            Directory.Delete(item, true);
                        }
                        else {
                            File.Move(item, destPath);
                        }
                    }

                    replacedCount++;
                    progress?.Report(new PluginsUpdateProgress(PluginsUpdateStage.Replacing, (float)replacedCount / totalPlugins * 100, string.Format(LanguageManager.Instance[nameof(Constants.I18n.PluginUpdate_ReplacedPlugin)], pluginName)));
                }

                // Step: Copy app_comp_manifest.json from extracted patch to both BaseDirectory and AppDataDir
                var extractedAppCompManifest = Path.Combine(extractDir, "app_comp_manifest.json");
                if (File.Exists(extractedAppCompManifest)) {
                    var baseManifest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_comp_manifest.json");
                    var appDataManifest = Path.Combine(Constants.CommonPaths.AppDataDir, "app_comp_manifest.json");
                    File.Copy(extractedAppCompManifest, baseManifest, true);
                    File.Copy(extractedAppCompManifest, appDataManifest, true);
                }

                // Step: Process removed plugins
                foreach (var pluginName in flag.RemovedPlugins) {
                    var pluginDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", pluginName));
                    if (Directory.Exists(pluginDir)) {
                        Directory.Delete(pluginDir, true);
                    }
                }

                // Refresh build info from manifest
                appBuildService.Refresh();

                // Step: Update flag to completed, then cleanup
                flag.Status = UpdateStatus.Completed;
                await SaveUpdateFlagAsync(flag, token);
                FileUtil.RemoveDirectory(pendingDir);
                FileUtil.RemoveDirectory(backupDir);

                result.Success = true;
                progress?.Report(new PluginsUpdateProgress(PluginsUpdateStage.Completed, 100, LanguageManager.Instance[nameof(Constants.I18n.PluginsUpdate_Stage_Completed)]));
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PluginsUpdateService>().Error("Plugins update failed", ex);
                progress?.Report(new PluginsUpdateProgress(PluginsUpdateStage.Failed, 100, LanguageManager.Instance[nameof(Constants.I18n.PluginsUpdate_Stage_Failed)]));
                result.Success = false;
                result.ErrorMessage = ex.Message;

                // Only rollback if backup exists (i.e. installation had started)
                var backupDir = Constants.CommonPaths.UpdateBackupDir;
                if (Directory.Exists(backupDir)) {
                    await RollbackAsync(flag);
                }
                else {
                    // No backup = verification failed before installation, just clean up
                    FileUtil.RemoveDirectory(Constants.CommonPaths.PendingPluginsUpdateDir);
                }
                await WriteUpdateFailedNoticeAsync(ex, token);
            }
            finally {
                UpdateLock.ReleaseAll();
                jobService.PluginsUpdateFinished();
            }

            return result;
        }

        public async Task<bool> CheckAndRecoverAsync(CancellationToken token = default) {
            var pendingDir = Constants.CommonPaths.PendingPluginsUpdateDir;
            if (!Directory.Exists(pendingDir)) return false;

            var flagPath = Constants.CommonPaths.UpdateFlagPath;
            if (!File.Exists(flagPath)) {
                // Flag missing but pending dir exists - cleanup
                FileUtil.RemoveDirectory(pendingDir);
                return false;
            }

            try {
                var flag = await LoadUpdateFlagAsync(token);
                if (flag == null) {
                    await RollbackAsync(null);
                    return false;
                }

                switch (flag.Status) {
                    case UpdateStatus.Pending:
                        return true;

                    case UpdateStatus.InProgress:
                        await RollbackAsync(flag);
                        return false;

                    case UpdateStatus.Completed:
                        FileUtil.RemoveDirectory(pendingDir);
                        FileUtil.RemoveDirectory(Constants.CommonPaths.UpdateBackupDir);
                        return false;
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PluginsUpdateService>().Error("Recovery check failed", ex);
                await RollbackAsync(null);
            }
            return false;
        }

        private void StopPlugins(IEnumerable<PluginName> plugins) {
            foreach (var plugin in plugins) {
                try { jobService.StopPlugin(plugin); }
                catch (Exception ex) { ArcLog.GetLogger<PluginsUpdateService>().Warn($"Failed to stop {plugin}: {ex.Message}"); }
            }
        }

        private static List<PluginName> ParsePluginNames(IEnumerable<string> names) =>
            names
                .Select(n => Enum.TryParse<PluginName>(n, true, out var p) ? (PluginName?)p : null)
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .ToList();

        private async Task RollbackAsync(PluginsUpdateFlag? flag) {
            ArcLog.GetLogger<PluginsUpdateService>().Warn("Start plugins rolling back");
            var backupDir = Constants.CommonPaths.UpdateBackupDir;
            var pendingDir = Constants.CommonPaths.PendingPluginsUpdateDir;
            if (!Directory.Exists(backupDir)) {
                ArcLog.GetLogger<PluginsUpdateService>().Warn("No backup found for rollback");
                FileUtil.RemoveDirectory(pendingDir);
                return;
            }

            try {
                // Restore each backed up plugin using target path from flag if available
                foreach (var backupPluginDir in Directory.GetDirectories(backupDir)) {
                    var pluginName = Path.GetFileName(backupPluginDir);
                    string targetDir;

                    if (flag != null && flag.Plugins.TryGetValue(pluginName, out var pluginInfo)) {
                        targetDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginInfo.Target));
                    }
                    else {
                        targetDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", pluginName));
                    }

                    // Clear current
                    if (Directory.Exists(targetDir)) {
                        FileUtil.DeleteDirectoryContents(targetDir);
                    }
                    else {
                        Directory.CreateDirectory(targetDir);
                    }

                    FileUtil.CopyDirectory(backupPluginDir, targetDir, true);
                }

                // Restore app_comp_manifest.json from backup to both BaseDirectory and AppDataDir
                var appCompManifestBackup = Path.Combine(backupDir, "app_comp_manifest.json");
                if (File.Exists(appCompManifestBackup)) {
                    var baseManifest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_comp_manifest.json");
                    var appDataManifest = Path.Combine(Constants.CommonPaths.AppDataDir, "app_comp_manifest.json");
                    File.Copy(appCompManifestBackup, baseManifest, true);
                    File.Copy(appCompManifestBackup, appDataManifest, true);
                }

                // Refresh build info from manifest
                appBuildService.Refresh();

                ArcLog.GetLogger<PluginsUpdateService>().Info("Rollback completed");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PluginsUpdateService>().Error("Rollback failed", ex);
            }
            finally {
                FileUtil.RemoveDirectory(pendingDir);
                FileUtil.RemoveDirectory(backupDir);
            }
        }

        private async Task WriteUpdateFailedNoticeAsync(Exception ex, CancellationToken token) {
            var notice = new UpdateFailedNotice {
                MessageKey = Constants.I18n.AppUpdater_UpdateFailedMessage,
                ExceptionMessage = ex.ToString()
            };
            var json = JsonSerializer.Serialize(notice, UpdateFailedNoticeContext.Default.UpdateFailedNotice);
            await File.WriteAllTextAsync(Constants.CommonPaths.UpdateFailedNoticePath, json, token);
        }

        private async Task<PluginsUpdateFlag?> LoadUpdateFlagAsync(CancellationToken token) {
            var flagPath = Constants.CommonPaths.UpdateFlagPath;
            if (!File.Exists(flagPath)) return null;

            var json = await File.ReadAllTextAsync(flagPath, token);
            return JsonSerializer.Deserialize(json, UpdateFlagContext.Default.PluginsUpdateFlag);
        }

        private async Task SaveUpdateFlagAsync(PluginsUpdateFlag flag, CancellationToken token) {
            var flagPath = Constants.CommonPaths.UpdateFlagPath;
            Directory.CreateDirectory(Path.GetDirectoryName(flagPath)!);
            var json = JsonSerializer.Serialize(flag, UpdateFlagContext.Default.PluginsUpdateFlag);
            await File.WriteAllTextAsync(flagPath, json, token);
        }

        public async Task ExecutePendingPluginUpdateWithWindowAsync() {
            // Prevent concurrent execution
            lock (_pendingLock) {
                if (_pendingUpdateTask != null && !_pendingUpdateTask.IsCompleted) return;
                _updateCts = new CancellationTokenSource();
                _pendingUpdateTask = ExecutePendingPluginUpdateCoreAsync(_updateCts.Token);
            }
            await _pendingUpdateTask;
        }

        private async Task ExecutePendingPluginUpdateCoreAsync(CancellationToken token) {
            try {
                var flagPath = Constants.CommonPaths.UpdateFlagPath;
                if (!File.Exists(flagPath)) return;

                // Create window on UI thread
                PluginUpdateWindow? progressWindow = null;
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    windowService.Show<Views.PluginUpdateWindow>(bringToFront: true);
                    windowService.TryGet(out progressWindow);
                });

                // Progress callback marshals to UI thread (non-blocking)
                var progress = new Progress<PluginsUpdateProgress>(p => {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() => {
                        progressWindow?.ReportProgress(p);
                    });
                });

                var result = await ExecutePendingUpdateAsync(progress, token);
                if (!result.Success) {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        progressWindow?.ShowError(result.ErrorMessage ?? "Unknown error");
                    });
                }
                else {
                    // Auto-close on success
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        progressWindow?.Close();
                    });
                }
            }
            catch (OperationCanceledException) {
                ArcLog.GetLogger<PluginsUpdateService>().Info("Plugin update cancelled");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PluginsUpdateService>().Error("Failed to execute pending plugin update", ex);
            }
        }

        private Task? _pendingUpdateTask;
        private readonly object _pendingLock = new();
        private CancellationTokenSource? _updateCts;
    }

    public class PluginsUpdateResult {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public record PluginsUpdateProgress(
        PluginsUpdateStage Stage,
        float Percent,
        string Message,
        float Speed = 0,
        IReadOnlyList<PluginDownloadProgress>? PluginDetails = null);

    public record PluginDownloadProgress(
        string PluginName,
        float Percent,
        long ReceivedBytes,
        long TotalBytes,
        float Speed) {
        public string SizeText => $"{FileUtil.SizeSuffix(ReceivedBytes)} / {FileUtil.SizeSuffix(TotalBytes)}";
    }

    public enum PluginsUpdateStage {
        Downloading,
        BackingUp,
        Replacing,
        Completed,
        Failed
    }
}
