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

namespace VirtualPaper.Cores.AppUpdate {
    public interface IPluginsUpdateService {
        Task<PluginsUpdateResult> ExecuteUpdateAsync(ReleaseInfo releaseInfo, IProgress<PluginsUpdateProgress>? progress = null, CancellationToken token = default);
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

    public class PluginsUpdateService : IPluginsUpdateService, IPluginsUpdateServiceInit {
        public PluginsUpdateService(
            IDownloadService downloadService,
            IJobService jobService,
            IAppBuildService appBuildService,
            IWindowService windowService) {
            _downloadService = downloadService;
            _jobService = jobService;
            _appBuildService = appBuildService;
            _windowService = windowService;
        }

        public async Task InitAsync() {
            UpdateLock.RegisterAll();

            var hasPending = await CheckAndRecoverAsync();
            if (hasPending) {
                await ExecutePendingPluginUpdateWithWindowAsync();
            }
            else {
                UpdateLock.ReleaseAll();
            }
            _appBuildService.Refresh();
        }

        public async Task<PluginsUpdateResult> ExecuteUpdateAsync(ReleaseInfo releaseInfo, IProgress<PluginsUpdateProgress>? progress = null, CancellationToken token = default) {
            var downloadResult = await DownloadPendingAsync(releaseInfo, null, token);
            if (!downloadResult.Success) return downloadResult;
            var verifyResult = await VerifyAndSavePendingAsync(releaseInfo, token);
            if (!verifyResult.Success) return verifyResult;
            return await ExecutePendingUpdateAsync(progress, token);
        }

        public async Task<PluginsUpdateResult> DownloadPendingAsync(ReleaseInfo releaseInfo, IProgress<DownloadProgress>? progress = null, CancellationToken token = default) {
            var result = new PluginsUpdateResult();

            if (releaseInfo.Manifest == null || !releaseInfo.Manifest.IsPluginsUpdate) {
                result.Success = false;
                result.ErrorMessage = "Not a restart-style update";
                return result;
            }

            var manifest = releaseInfo.Manifest;
            var pendingDir = Constants.CommonPaths.PendingUpdatesDir;

            try {
                FileUtil.RemoveDirectory(pendingDir);
                Directory.CreateDirectory(pendingDir);

                var downloadItems = new List<(Uri uri, string saveFilePath)>();

                foreach (var kv in manifest.Plugins) {
                    var pluginName = kv.Key;
                    var pluginInfo = kv.Value;

                    if (!releaseInfo.PluginAssetUris.TryGetValue(pluginName, out var downloadUri)) {
                        throw new InvalidOperationException($"Download URI not found for plugin: {pluginName}");
                    }

                    var pluginDir = Path.Combine(pendingDir, pluginName);
                    Directory.CreateDirectory(pluginDir);
                    var zipPath = Path.Combine(pluginDir, pluginInfo.Asset);

                    downloadItems.Add((downloadUri, zipPath));
                }

                await foreach (var p in _downloadService.DownloadMultipleAsync(downloadItems, token)) {
                    progress?.Report(p);
                }

                // Save manifest to pending dir for later copy to installation root
                var manifestJson = JsonSerializer.Serialize(manifest, UpdateManifestContext.Default.UpdateManifest);
                var manifestPath = Path.Combine(pendingDir, "app_manifest.json");
                await File.WriteAllTextAsync(manifestPath, manifestJson, token);

                result.Success = true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PluginsUpdateService>().Error("Restart update download failed", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                FileUtil.RemoveDirectory(pendingDir);
            }

            return result;
        }

        public async Task<PluginsUpdateResult> VerifyAndSavePendingAsync(ReleaseInfo releaseInfo, CancellationToken token = default) {
            var result = new PluginsUpdateResult();

            if (releaseInfo.Manifest == null || !releaseInfo.Manifest.IsPluginsUpdate) {
                result.Success = false;
                result.ErrorMessage = "Not a restart-style update";
                return result;
            }

            var manifest = releaseInfo.Manifest;
            var pendingDir = Constants.CommonPaths.PendingUpdatesDir;

            try {
                foreach (var kv in manifest.Plugins) {
                    var pluginName = kv.Key;
                    var pluginInfo = kv.Value;
                    var zipPath = Path.Combine(pendingDir, pluginName, pluginInfo.Asset);

                    bool verified = await _downloadService.VerifyFileIntegrityAsync(zipPath, pluginInfo.Sha256, token);
                    if (!verified) {
                        throw new InvalidDataException($"SHA256 verification failed for plugin: {pluginName}");
                    }
                }

                var updateFlag = new UpdateFlag {
                    Status = UpdateFlag.UpdateStatusPending,
                    AppBuild = manifest.AppBuild,
                    Plugins = manifest.Plugins.ToDictionary(
                        kv => kv.Key,
                        kv => new PluginFlagInfo {
                            Target = Path.Combine("Plugins", kv.Key),
                            Build = kv.Value.Build,
                            Files = new List<FileHashInfo> {
                                new FileHashInfo {
                                    Name = kv.Value.Asset,
                                    Sha256 = kv.Value.Sha256
                                }
                            }
                        }),
                    AppPluginsInfo = manifest.AppPluginsInfo,
                    RemovedPlugins = manifest.RemovedPlugins
                };
                await SaveUpdateFlagAsync(updateFlag, token);

                result.Success = true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PluginsUpdateService>().Error("Restart update verify failed", ex);
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
            var pendingDir = Constants.CommonPaths.PendingUpdatesDir;
            var flagPath = Constants.CommonPaths.UpdateFlagPath;

            if (!File.Exists(flagPath)) {
                result.Success = false;
                result.ErrorMessage = "No pending update found";
                return result;
            }

            var flag = await LoadUpdateFlagAsync(token);
            if (flag == null || flag.Status != UpdateFlag.UpdateStatusPending) {
                result.Success = false;
                result.ErrorMessage = "No pending update found or invalid state";
                return result;
            }

            try {
                // Verify downloaded files against hashes in flag (single verification pass)
                foreach (var (pluginName, pluginInfo) in flag.Plugins) {
                    foreach (var fileHash in pluginInfo.Files) {
                        var filePath = Path.Combine(pendingDir, pluginName, fileHash.Name);
                        if (!File.Exists(filePath)) {
                            throw new FileNotFoundException($"Pending update file missing: {filePath}");
                        }
                        bool verified = await _downloadService.VerifyFileIntegrityAsync(filePath, fileHash.Sha256, token);
                        if (!verified) {
                            throw new InvalidDataException($"Pending update file verification failed: {filePath}");
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

                foreach (var (pluginName, pluginInfo) in flag.Plugins) {
                    var sourceDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginInfo.Target));
                    if (Directory.Exists(sourceDir)) {
                        var backupPath = Path.Combine(backupDir, pluginName);
                        FileUtil.CopyDirectory(sourceDir, backupPath, true);
                    }
                }

                // Backup app_manifest.json from WorkSpace root
                var appManifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_manifest.json");
                if (File.Exists(appManifestPath)) {
                    var appManifestBackup = Path.Combine(backupDir, "app_manifest.json");
                    File.Copy(appManifestPath, appManifestBackup, true);
                }

                // Step: Update flag to in_progress
                flag.Status = UpdateFlag.UpdateStatusInProgress;
                await SaveUpdateFlagAsync(flag, token);

                // Step: Replace plugins sequentially (avoid IO contention on Windows)
                progress?.Report(new PluginsUpdateProgress(PluginsUpdateStage.Replacing, 0, LanguageManager.Instance[nameof(Constants.I18n.PluginsUpdate_Stage_Replacing)]));
                int totalPlugins = flag.Plugins.Count;
                int replacedCount = 0;

                foreach (var (pluginName, pluginInfo) in flag.Plugins) {
                    token.ThrowIfCancellationRequested();

                    var targetDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginInfo.Target));
                    var zipPath = Path.Combine(pendingDir, pluginName, pluginInfo.Files[0].Name);

                    // Clear target directory
                    if (Directory.Exists(targetDir)) {
                        FileUtil.DeleteDirectoryContents(targetDir);
                    }
                    else {
                        Directory.CreateDirectory(targetDir);
                    }

                    // Extract zip - the zip contains a single folder with the plugin name
                    var extractDir = Path.Combine(pendingDir, pluginName, "extracted");
                    if (Directory.Exists(extractDir)) {
                        Directory.Delete(extractDir, true);
                    }
                    Directory.CreateDirectory(extractDir);
                    ZipFile.ExtractToDirectory(zipPath, extractDir, true);

                    // Find the plugin folder inside the extracted content
                    var folders = Directory.GetDirectories(extractDir);
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

                // Step: Copy app_manifest.json from pending to installation root
                var pendingManifest = Path.Combine(pendingDir, "app_manifest.json");
                if (File.Exists(pendingManifest)) {
                    var installManifest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_manifest.json");
                    File.Copy(pendingManifest, installManifest, true);
                }

                // Step: Process removed plugins
                foreach (var pluginName in flag.RemovedPlugins) {
                    var pluginDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", pluginName));
                    if (Directory.Exists(pluginDir)) {
                        Directory.Delete(pluginDir, true);
                    }
                }

                // Refresh build info from manifest
                _appBuildService.Refresh();

                // Step: Update flag to completed, then cleanup
                flag.Status = UpdateFlag.UpdateStatusCompleted;
                await SaveUpdateFlagAsync(flag, token);
                FileUtil.RemoveDirectory(pendingDir);
                FileUtil.RemoveDirectory(backupDir);

                result.Success = true;
                progress?.Report(new PluginsUpdateProgress(PluginsUpdateStage.Completed, 100, LanguageManager.Instance[nameof(Constants.I18n.PluginsUpdate_Stage_Completed)]));
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PluginsUpdateService>().Error("Plugins update failed", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;

                // Rollback
                await RollbackAsync(flag);

                // Write rollback notice
                await WriteRollbackNoticeAsync(token);
            }
            finally {
                UpdateLock.ReleaseAll();
                _jobService.PluginsUpdateFinished();
            }

            return result;
        }

        public async Task<bool> CheckAndRecoverAsync(CancellationToken token = default) {
            var pendingDir = Constants.CommonPaths.PendingUpdatesDir;
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
                    case UpdateFlag.UpdateStatusPending:
                        return true;

                    case UpdateFlag.UpdateStatusInProgress:
                        await RollbackAsync(flag);
                        return false;

                    case UpdateFlag.UpdateStatusCompleted:
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
                try { _jobService.StopPlugin(plugin); }
                catch (Exception ex) { ArcLog.GetLogger<PluginsUpdateService>().Warn($"Failed to stop {plugin}: {ex.Message}"); }
            }
        }

        private static List<PluginName> ParsePluginNames(IEnumerable<string> names) =>
            names
                .Select(n => Enum.TryParse<PluginName>(n, true, out var p) ? (PluginName?)p : null)
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .ToList();

        private async Task RollbackAsync(UpdateFlag? flag) {
            ArcLog.GetLogger<PluginsUpdateService>().Warn("Plugins update error, rolling back");
            var backupDir = Constants.CommonPaths.UpdateBackupDir;
            var pendingDir = Constants.CommonPaths.PendingUpdatesDir;
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

                // Restore app_manifest.json from backup
                var appManifestBackup = Path.Combine(backupDir, "app_manifest.json");
                if (File.Exists(appManifestBackup)) {
                    var appManifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_manifest.json");
                    File.Copy(appManifestBackup, appManifestPath, true);
                }

                // Refresh build info from manifest
                _appBuildService.Refresh();

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

        private async Task WriteRollbackNoticeAsync(CancellationToken token) {
            var notice = new RollbackNotice {
                Rollback = true,
                MessageKey = Constants.I18n.AppUpdater_RollbackMessage
            };
            var json = JsonSerializer.Serialize(notice, RollbackNoticeContext.Default.RollbackNotice);
            await File.WriteAllTextAsync(Constants.CommonPaths.RollbackNoticePath, json, token);
        }

        private async Task<UpdateFlag?> LoadUpdateFlagAsync(CancellationToken token) {
            var flagPath = Constants.CommonPaths.UpdateFlagPath;
            if (!File.Exists(flagPath)) return null;

            var json = await File.ReadAllTextAsync(flagPath, token);
            return JsonSerializer.Deserialize(json, UpdateFlagContext.Default.UpdateFlag);
        }

        private async Task SaveUpdateFlagAsync(UpdateFlag flag, CancellationToken token) {
            var flagPath = Constants.CommonPaths.UpdateFlagPath;
            Directory.CreateDirectory(Path.GetDirectoryName(flagPath)!);
            var json = JsonSerializer.Serialize(flag, UpdateFlagContext.Default.UpdateFlag);
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
                    _windowService.Show<Views.PluginUpdateWindow>(bringToFront: true);
                    _windowService.TryGet(out progressWindow);
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

        private readonly IDownloadService _downloadService;
        private readonly IJobService _jobService;
        private readonly IAppBuildService _appBuildService;
        private readonly IWindowService _windowService;

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
