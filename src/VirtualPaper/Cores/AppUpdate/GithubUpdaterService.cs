using System.IO;
using System.Text.Json;
using Octokit;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Models.Events;
using VirtualPaper.Utils.Interfcaes;
using Timer = System.Timers.Timer;

namespace VirtualPaper.Cores.AppUpdate {
    public sealed class GithubUpdaterService : IAppUpdaterService {
        public event EventHandler<AppUpdaterEventArgs>? UpdateChecked;

        public AppUpdateStatus Status { get; private set; } = AppUpdateStatus.Notchecked;
        public ReleaseInfo? LastReleaseInfo { get; private set; }

        public GithubUpdaterService(
            IGithubReleaseClient githubReleaseClient,
            IVersionComparer versionComparer,
            IAppBuildService appBuildService) {
            _githubReleaseClient = githubReleaseClient;
            _versionComparer = versionComparer;
            _appBuildService = appBuildService;

            _retryTimer.Elapsed += RetryTimer_Elapsed;
            //giving the retry delay is not reliable since it will reset if system sleeps/suspends.
            _retryTimer.Interval = 5 * 60 * 1000;
        }

        public async Task<AppUpdateStatus> CheckUpdateAsync(int fetchDelay = 45000) {
            if (Constants.ApplicationType.IsMSIX) {
                //msix already has built-in _updater.
                return AppUpdateStatus.Notchecked;
            }

            var localStatus = ProbeLocalUpdate();
            if (localStatus != null) {
                Status = localStatus.Value;
                LastReleaseInfo = new() {
                    CheckedTime = DateTime.Now
                };
                UpdateChecked?.Invoke(this, new AppUpdaterEventArgs(Status, LastReleaseInfo));
                return Status;
            }

            try {
                await Task.Delay(fetchDelay);
                var releaseInfo = await _githubReleaseClient.GetLatestRelease(Constants.ApplicationType.IsTestBuild);
                LastReleaseInfo = releaseInfo;
                LastReleaseInfo.CheckedTime = DateTime.Now;

                int verCompare = _versionComparer.CompareAssemblyVersion(releaseInfo.Version);
                if (verCompare > 0) {
                    //update Available.
                    Status = AppUpdateStatus.Available;
                }
                else if (releaseInfo.IsPluginsUpdate && releaseInfo.PendingManifest != null && HasPluginUpdate(releaseInfo)) {
                    //version unchanged, but plugin updates available.
                    Status = AppUpdateStatus.Available;
                }
                else if (verCompare < 0 || releaseInfo.InstallerUri == null) {
                    //beta release.
                    Status = AppUpdateStatus.Invalid;
                }
                else {
                    //up-to-date.
                    Status = AppUpdateStatus.Uptodate;
                }
            }
            catch (RateLimitExceededException e) {
                ArcLog.GetLogger<GithubUpdaterService>().Warn("Github rate limit exceeded, retry after reset");
                LastReleaseInfo ??= new ReleaseInfo();
                LastReleaseInfo.CheckedTime = DateTime.Now;
                Status = AppUpdateStatus.Error;
                if (e.HttpResponse?.Headers.TryGetValue("X-RateLimit-Reset", out var resetStr) == true
                    && long.TryParse(resetStr, out var resetUnix)) {
                    var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnix);
                    var delay = resetTime - DateTimeOffset.UtcNow;
                    if (delay > TimeSpan.Zero) {
                        _retryTimer.Interval = delay.TotalMilliseconds + 60_000;
                    }
                }
            }
            catch (Exception e) {
                ArcLog.GetLogger<GithubUpdaterService>().Error("Github update fetch failed", e);
                LastReleaseInfo ??= new ReleaseInfo();
                LastReleaseInfo.CheckedTime = DateTime.Now;
                Status = AppUpdateStatus.Error;
            }

            UpdateChecked?.Invoke(this, new AppUpdaterEventArgs(Status, LastReleaseInfo));
            return Status;
        }

        /// <summary>
        /// 本地探测：检查 pending_updates 和 installer_cache 是否有已就绪的更新。
        /// 验证失败则清理对应目录。
        /// </summary>
        private AppUpdateStatus? ProbeLocalUpdate() {
            // 1. 检查 restart-style: pending_updates + update.flag (pending status)
            if (ProbePluginsReady())
                return AppUpdateStatus.PluginsReady;

            // 2. 检查 install-style: installer_cache 有文件 + sha256 验证通过
            if (ProbeInstallerReady())
                return AppUpdateStatus.InstallerReady;

            return null;
        }

        private bool ProbePluginsReady() {
            try {
                var flagPath = Constants.CommonPaths.UpdateFlagPath;
                if (!File.Exists(flagPath)) return false;

                var json = File.ReadAllText(flagPath);
                var flag = JsonSerializer.Deserialize(json, UpdateFlagContext.Default.UpdateFlag);
                if (flag == null || flag.Status != UpdateFlag.UpdateStatusPending) {
                    FileUtil.RemoveDirectory(Constants.CommonPaths.PendingUpdatesDir);
                    return false;
                }

                var extractDir = Constants.CommonPaths.PluginPatchExtractDir;
                if (!Directory.Exists(extractDir)) {
                    FileUtil.RemoveDirectory(Constants.CommonPaths.PendingUpdatesDir);
                    return false;
                }

                foreach (var kv in flag.Plugins) {
                    foreach (var fileHash in kv.Value.Files) {
                        var filePath = Path.Combine(extractDir, fileHash.Name);
                        if (!FileUtil.VerifyFileIntegrityAsync(filePath, fileHash.Sha256).GetAwaiter().GetResult()) {
                            FileUtil.RemoveDirectory(Constants.CommonPaths.PendingUpdatesDir);
                            return false;
                        }
                    }
                }
                return true;
            }
            catch {
                FileUtil.RemoveDirectory(Constants.CommonPaths.PendingUpdatesDir);
                return false;
            }
        }

        private bool ProbeInstallerReady() {
            try {
                var cacheDir = Constants.CommonPaths.InstallerCacheDir;
                if (!Directory.Exists(cacheDir)) return false;

                foreach (var file in Directory.GetFiles(cacheDir)) {
                    if (file.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)) continue;

                    var shaPath = file + ".sha256";
                    if (!File.Exists(shaPath)) continue;

                    var expectedSha = File.ReadAllText(shaPath).Trim();
                    if (FileUtil.VerifyFileIntegrityAsync(file, expectedSha).GetAwaiter().GetResult())
                        return true;
                }

                // 有文件但无有效配对 → 清理
                FileUtil.RemoveDirectory(Constants.CommonPaths.InstallerCacheDir);
                return false;
            }
            catch {
                FileUtil.RemoveDirectory(Constants.CommonPaths.InstallerCacheDir);
                return false;
            }
        }

        /// <summary>
        /// Check for updates periodically.
        /// </summary>
        public void Start() {
            _retryTimer.Start();
        }

        /// <summary>
        /// Stops periodic updates check.
        /// </summary>
        public void Stop() {
            if (_retryTimer.Enabled) {
                _retryTimer.Stop();
            }
        }

        #region private
        private void RetryTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e) {
            if (LastReleaseInfo == null || (DateTime.Now - LastReleaseInfo.CheckedTime).TotalMilliseconds > (Status != AppUpdateStatus.Error ? _fetchDelayRepeat : _fetchDelayError)) {
                _ = CheckUpdateAsync(0);
            }
        }

        private bool HasPluginUpdate(ReleaseInfo releaseInfo) {
            foreach (var (pluginName, pluginInfo) in releaseInfo.PendingManifest!.Plugins) {
                var localBuild = _appBuildService.GetPluginBuild(pluginName);
                if (!string.IsNullOrEmpty(localBuild) &&
                    string.Compare(pluginInfo.BuildNumber, localBuild, StringComparison.Ordinal) > 0) {
                    ArcLog.GetLogger<GithubUpdaterService>().Info($"Plugin update available: {pluginName} ({localBuild} -> {pluginInfo.BuildNumber})");
                    return true;
                }
            }
            return false;
        }
        #endregion

        private readonly int _fetchDelayError = 30 * 60 * 1000; //30min
        private readonly int _fetchDelayRepeat = 12 * 60 * 60 * 1000; //12hr
        private readonly Timer _retryTimer = new();
        private readonly IGithubReleaseClient _githubReleaseClient;
        private readonly IVersionComparer _versionComparer;
        private readonly IAppBuildService _appBuildService;
    }
}
