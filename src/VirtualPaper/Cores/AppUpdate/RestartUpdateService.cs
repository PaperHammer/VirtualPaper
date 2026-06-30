using System.IO;
using System.IO.Compression;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Cores.ScreenSaver;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Cores.AppUpdate {
    public interface IRestartUpdateService {
        Task<RestartUpdateResult> ExecuteUpdateAsync(ReleaseInfo releaseInfo, IProgress<RestartUpdateProgress>? progress = null, CancellationToken token = default);
        Task<RestartUpdateResult> DownloadPendingAsync(ReleaseInfo releaseInfo, IProgress<DownloadProgress>? progress = null, CancellationToken token = default);
        Task<RestartUpdateResult> VerifyAndSavePendingAsync(ReleaseInfo releaseInfo, CancellationToken token = default);
        Task<RestartUpdateResult> ExecutePendingUpdateAsync(IProgress<RestartUpdateProgress>? progress = null, CancellationToken token = default);
        Task CheckAndRecoverAsync(CancellationToken token = default);
    }

    public class RestartUpdateService : IRestartUpdateService {
        public RestartUpdateService(
            IDownloadService downloadService,
            IWallpaperControl wallpaperControl,
            IScrControl scrControl,
            IUIRunnerService uiRunnerService,
            IAppBuildService appBuildService) {
            _downloadService = downloadService;
            _wallpaperControl = wallpaperControl;
            _scrControl = scrControl;
            _uiRunnerService = uiRunnerService;
            _appBuildService = appBuildService;
        }

        public async Task<RestartUpdateResult> ExecuteUpdateAsync(ReleaseInfo releaseInfo, IProgress<RestartUpdateProgress>? progress = null, CancellationToken token = default) {
            var downloadResult = await DownloadPendingAsync(releaseInfo, null, token);
            if (!downloadResult.Success) return downloadResult;
            var verifyResult = await VerifyAndSavePendingAsync(releaseInfo, token);
            if (!verifyResult.Success) return verifyResult;
            return await ExecutePendingUpdateAsync(progress, token);
        }

        public async Task<RestartUpdateResult> DownloadPendingAsync(ReleaseInfo releaseInfo, IProgress<DownloadProgress>? progress = null, CancellationToken token = default) {
            var result = new RestartUpdateResult();

            if (releaseInfo.Manifest == null || !releaseInfo.Manifest.IsRestartUpdate) {
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

                result.Success = true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<RestartUpdateService>().Error("Restart update download failed", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                FileUtil.RemoveDirectory(pendingDir);
            }

            return result;
        }

        public async Task<RestartUpdateResult> VerifyAndSavePendingAsync(ReleaseInfo releaseInfo, CancellationToken token = default) {
            var result = new RestartUpdateResult();

            if (releaseInfo.Manifest == null || !releaseInfo.Manifest.IsRestartUpdate) {
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
                    RemovedPlugins = manifest.RemovedPlugins
                };
                await SaveUpdateFlagAsync(updateFlag, token);

                result.Success = true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<RestartUpdateService>().Error("Restart update verify failed", ex);
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
        public async Task<RestartUpdateResult> ExecutePendingUpdateAsync(IProgress<RestartUpdateProgress>? progress = null, CancellationToken token = default) {
            var result = new RestartUpdateResult();
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
                // Verify downloaded files against hashes in flag
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

                // Lock: prevent specific plugin startup
                var updatingPluginNames = flag.Plugins.Keys.ToList();
                if (!updatingPluginNames.Contains("UI")) {
                    updatingPluginNames.Add("UI");
                }
                UpdateLock.SetUpdatingPlugins(updatingPluginNames);

                // Stop plugins (always stops UI, plus others if being updated)
                StopPlugins(flag.Plugins.Keys.ToList());

                // Step: Backup current plugins
                progress?.Report(new RestartUpdateProgress(RestartUpdateStage.BackingUp, 0, "Backing up current plugins..."));
                var backupDir = Constants.CommonPaths.UpdateBackupDir;
                Directory.CreateDirectory(backupDir);

                foreach (var (pluginName, pluginInfo) in flag.Plugins) {
                    var sourceDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginInfo.Target));
                    if (Directory.Exists(sourceDir)) {
                        var backupPath = Path.Combine(backupDir, pluginName);
                        FileUtil.CopyDirectory(sourceDir, backupPath, true);
                    }
                }

                // Step: Update flag to in_progress
                flag.Status = UpdateFlag.UpdateStatusInProgress;
                await SaveUpdateFlagAsync(flag, token);

                // Step: Replace plugins in parallel
                progress?.Report(new RestartUpdateProgress(RestartUpdateStage.Replacing, 0, "Replacing plugins files..."));
                int totalPlugins = flag.Plugins.Count;
                int replacedCount = 0;

                var replaceTasks = flag.Plugins.Select(async kv => {
                    var (pluginName, pluginInfo) = kv;
                    token.ThrowIfCancellationRequested();

                    var targetDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginInfo.Target));
                    var zipPath = Path.Combine(pendingDir, pluginName, pluginInfo.Files[0].Name);

                    // Verify zip SHA256 against manifest
                    var actualHash = FileUtil.GetChecksumSHA256(zipPath);
                    var expectedHash = pluginInfo.Files[0].Sha256;
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase)) {
                        throw new InvalidOperationException($"SHA256 mismatch for {pluginName}: expected {expectedHash}, got {actualHash}");
                    }

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

                    var count = Interlocked.Increment(ref replacedCount);
                    progress?.Report(new RestartUpdateProgress(RestartUpdateStage.Replacing, (float)count / totalPlugins * 100, $"Replaced {pluginName}"));
                });

                await Task.WhenAll(replaceTasks);

                // Step: Update app_build.json for all replaced plugins
                foreach (var (pluginName, pluginInfo) in flag.Plugins) {
                    _appBuildService.BuildInfo.Plugins[pluginName] = pluginInfo.Build;
                }
                await _appBuildService.SaveAsync();

                // Step: Process removed plugins
                foreach (var pluginName in flag.RemovedPlugins) {
                    var pluginDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", pluginName));
                    if (Directory.Exists(pluginDir)) {
                        Directory.Delete(pluginDir, true);
                    }
                    _appBuildService.BuildInfo.Plugins.Remove(pluginName);
                }
                if (flag.RemovedPlugins.Count > 0) {
                    await _appBuildService.SaveAsync();
                }

                // Step: Update flag to completed, then cleanup
                flag.Status = UpdateFlag.UpdateStatusCompleted;
                await SaveUpdateFlagAsync(flag, token);
                FileUtil.RemoveDirectory(pendingDir);

                result.Success = true;
                progress?.Report(new RestartUpdateProgress(RestartUpdateStage.Completed, 100, "Update completed"));
            }
            catch (Exception ex) {
                ArcLog.GetLogger<RestartUpdateService>().Error("Restart update failed", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;

                // Rollback
                await RollbackAsync();

                // Write rollback notice
                await WriteRollbackNoticeAsync(token);
            }
            finally {
                // Unlock
                UpdateLock.ClearUpdatingPlugins();
                // Always restart UI after restart-style update
                _uiRunnerService.ShowUI();
            }

            return result;
        }

        public async Task CheckAndRecoverAsync(CancellationToken token = default) {
            var pendingDir = Constants.CommonPaths.PendingUpdatesDir;
            if (!Directory.Exists(pendingDir)) return;

            var flagPath = Constants.CommonPaths.UpdateFlagPath;
            if (!File.Exists(flagPath)) {
                // Flag missing but pending dir exists - cleanup
                FileUtil.RemoveDirectory(pendingDir);
                return;
            }

            try {
                var flag = await LoadUpdateFlagAsync(token);
                if (flag == null) {
                    // Flag corrupted - rollback if backup exists
                    UpdateLock.SetUpdatingPlugins(GetAllPluginNames());
                    StopPlugins(GetAllPluginNames());
                    await RollbackAsync();
                    UpdateLock.ClearUpdatingPlugins();
                    _uiRunnerService.ShowUI();
                    return;
                }

                switch (flag.Status) {
                    case UpdateFlag.UpdateStatusPending:
                        // Pending update - execute it (files already downloaded and verified)
                        await ExecutePendingUpdateAsync();
                        break;

                    case UpdateFlag.UpdateStatusInProgress:
                        // Crashed during update - rollback
                        var crashedPlugins = flag.Plugins.Keys.ToList();
                        UpdateLock.SetUpdatingPlugins(crashedPlugins);
                        StopPlugins(crashedPlugins);
                        await RollbackAsync();
                        UpdateLock.ClearUpdatingPlugins();
                        // Always restart UI after recovery
                        _uiRunnerService.ShowUI();
                        break;

                    case UpdateFlag.UpdateStatusCompleted:
                        // Completed but not cleaned up - just cleanup
                        FileUtil.RemoveDirectory(pendingDir);
                        break;
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<RestartUpdateService>().Error("Recovery check failed", ex);
                UpdateLock.SetUpdatingPlugins(GetAllPluginNames());
                StopPlugins(GetAllPluginNames());
                await RollbackAsync();
                UpdateLock.ClearUpdatingPlugins();
                _uiRunnerService.ShowUI();
            }
        }

        private void StopPlugins(IEnumerable<string> pluginNames) {
            var pluginList = pluginNames.Select(n => n.ToUpperInvariant()).ToHashSet();

            // Always stop UI for restart-style update
            try { _uiRunnerService.CloseUI(); }
            catch (Exception ex) { ArcLog.GetLogger<RestartUpdateService>().Warn($"Failed to stop UI: {ex.Message}"); }

            // Stop PlayerWeb if it's being updated
            if (pluginList.Contains("PLAYERWEB")) {
                try { _wallpaperControl.CloseAllWallpapers(); }
                catch (Exception ex) { ArcLog.GetLogger<RestartUpdateService>().Warn($"Failed to stop PlayerWeb: {ex.Message}"); }
            }

            // Stop ScrSaver if it's being updated
            if (pluginList.Contains("SCRSAVER")) {
                try { _scrControl.Stop(); }
                catch (Exception ex) { ArcLog.GetLogger<RestartUpdateService>().Warn($"Failed to stop ScrSaver: {ex.Message}"); }
            }

            // ML and Shaders don't need explicit stop - they're loaded by UI/PlayerWeb
        }

        private static IEnumerable<string> GetAllPluginNames() => new[] { "UI", "PlayerWeb", "ScrSaver", "ML", "Shaders" };

        private async Task RollbackAsync() {
            var backupDir = Constants.CommonPaths.UpdateBackupDir;
            var pendingDir = Constants.CommonPaths.PendingUpdatesDir;
            if (!Directory.Exists(backupDir)) {
                ArcLog.GetLogger<RestartUpdateService>().Warn("No backup found for rollback");
                FileUtil.RemoveDirectory(pendingDir);
                return;
            }

            try {
                // Restore each backed up plugin
                foreach (var backupPluginDir in Directory.GetDirectories(backupDir)) {
                    var pluginName = Path.GetFileName(backupPluginDir);
                    var targetDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", pluginName));

                    // Clear current
                    if (Directory.Exists(targetDir)) {
                        FileUtil.DeleteDirectoryContents(targetDir);
                    }
                    else {
                        Directory.CreateDirectory(targetDir);
                    }

                    FileUtil.CopyDirectory(backupPluginDir, targetDir, true);
                }

                ArcLog.GetLogger<RestartUpdateService>().Info("Rollback completed");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<RestartUpdateService>().Error("Rollback failed", ex);
            }
            finally {
                FileUtil.RemoveDirectory(pendingDir);
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

        private readonly IDownloadService _downloadService;
        private readonly IWallpaperControl _wallpaperControl;
        private readonly IScrControl _scrControl;
        private readonly IUIRunnerService _uiRunnerService;
        private readonly IAppBuildService _appBuildService;
    }

    public class RestartUpdateResult {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public record RestartUpdateProgress(
        RestartUpdateStage Stage,
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

    public enum RestartUpdateStage {
        Downloading,
        BackingUp,
        Replacing,
        Completed,
        Failed
    }
}
