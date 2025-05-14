using System.Diagnostics;
using VirtualPaper.Common;
using VirtualPaper.Common.Events;
using VirtualPaper.Common.Utils;
using Timer = System.Timers.Timer;

namespace VirtualPaper.Cores.AppUpdate {
    public sealed class GithubUpdaterService : IAppUpdaterService {
        public event EventHandler<AppUpdaterEventArgs>? UpdateChecked;

        public string LastCheckChangelog { get; private set; } = string.Empty;
        public DateTime LastCheckTime { get; private set; } = DateTime.MinValue;
        public Uri LastCheckUri { get; private set; }
        public Version LastCheckVersion { get; private set; } = new Version(0, 0, 0, 0);
        public AppUpdateStatus Status { get; private set; } = AppUpdateStatus.Notchecked;

        public GithubUpdaterService() {
            _retryTimer.Elapsed += RetryTimer_Elapsed;
            //giving the retry delay is not reliable since it will reset if system sleeps/suspends.
            _retryTimer.Interval = 5 * 60 * 1000;
        }

        public async Task<AppUpdateStatus> CheckUpdate(int fetchDelay = 45000) {
            if (Constants.ApplicationType.IsMSIX) {
                //msix already has built-in _updater.
                return AppUpdateStatus.Notchecked;
            }

            try {
                await Task.Delay(fetchDelay);
                (Uri, Version, string) data = await GetLatestRelease(Constants.ApplicationType.IsTestBuild);
                int verCompare = GithubUtil.CompareAssemblyVersion(data.Item2);
                if (verCompare > 0) {
                    //update Available.
                    Status = AppUpdateStatus.Available;
                }
                else if (verCompare < 0) {
                    //beta release.
                    Status = AppUpdateStatus.Invalid;
                }
                else {
                    //up-to-date.
                    Status = AppUpdateStatus.Uptodate;
                }
                LastCheckUri = data.Item1;
                LastCheckVersion = data.Item2;
                LastCheckChangelog = data.Item3;
            }
            catch (Exception e) {
                Debug.WriteLine("Update fetch Error:" + e.ToString());
                Status = AppUpdateStatus.Error;
            }
            LastCheckTime = DateTime.Now;

            UpdateChecked?.Invoke(this, new AppUpdaterEventArgs(Status, LastCheckVersion, LastCheckTime, LastCheckUri, LastCheckChangelog));
            return Status;
        }

        private async Task<List<(Uri, Version, string)>> GetModulesLatestRelease(bool isBeta) {
            var userName = "PaperHammer";
            var repositoryName = isBeta ? "VirtualPaper-beta" : "VirtualPaper";
            var gitRelease = await GithubUtil.GetLatestRelease(repositoryName, userName, 0);
            Version version = GithubUtil.GetVersion(gitRelease);

            //download asset format: virtualpaper_x64_module_YYY_vXXXX.dll, YYY - module-name, XXXX - 4 digit version no.
            var gitUrls = await GithubUtil.GetAllAssetUrl(
                "virtualpaper_x64_module",
                gitRelease, repositoryName, userName);
            List<(Uri, Version, string)> res = [];
            foreach (var url in gitUrls) { 
                Uri uri = new(url);
                string changelog = gitRelease.Body;
                res.Add((uri, version, changelog));
            }

            return res;
        }

        public async Task<(Uri, Version, string)> GetLatestRelease(bool isBeta) {
            var userName = "PaperHammer";
            var repositoryName = isBeta ? "VirtualPaper-beta" : "VirtualPaper";
            var gitRelease = await GithubUtil.GetLatestRelease(repositoryName, userName, 0);
            Version version = GithubUtil.GetVersion(gitRelease);

            //download asset format: virtualpaper_setup_x64_full_vXXXX.exe, XXXX - 4 digit version no.
            var gitUrl = await GithubUtil.GetAssetUrl(
                "virtualpaper_setup_x64_full",
                gitRelease, repositoryName, userName);
            Uri uri = new(gitUrl);
            string changelog = gitRelease.Body;

            return (uri, version, changelog);
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
            if ((DateTime.Now - LastCheckTime).TotalMilliseconds > (Status != AppUpdateStatus.Error ? _fetchDelayRepeat : _fetchDelayError)) {
                _ = CheckUpdate(0);
            }
        }
        #endregion

        private readonly int _fetchDelayError = 30 * 60 * 1000; //30min
        private readonly int _fetchDelayRepeat = 12 * 60 * 60 * 1000; //12hr
        private readonly Timer _retryTimer = new();
    }
}
