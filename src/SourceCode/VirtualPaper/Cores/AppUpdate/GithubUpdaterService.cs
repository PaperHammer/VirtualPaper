using System.Diagnostics;
using VirtualPaper.Common;
using VirtualPaper.Common.Models;
using VirtualPaper.Common.Utils;
using Timer = System.Timers.Timer;

namespace VirtualPaper.Cores.AppUpdate {
    public sealed class GithubUpdaterService : IAppUpdaterService {
        public event EventHandler<AppUpdaterEventArgs>? UpdateChecked;

        public string LastCheckChangelog { get; private set; } = string.Empty;
        public DateTime LastCheckTime { get; private set; } = DateTime.MinValue;
        public Uri LastCheckUri { get; private set; }
        public Version LastCheckVersion { get; private set; } = new Version(0, 0, 0, 0);
        public AppUpdateStatus Status { get; private set; } = AppUpdateStatus.notchecked;

        public GithubUpdaterService() {
            _retryTimer.Elapsed += RetryTimer_Elapsed;
            //giving the retry delay is not reliable since it will reset if system sleeps/suspends.
            _retryTimer.Interval = 5 * 60 * 1000;
        }

        public async Task<AppUpdateStatus> CheckUpdate(int fetchDelay = 45 * 1000) {
            if (Constants.ApplicationType.IsMSIX) {
                //msix already has built-in _updater.
                return AppUpdateStatus.notchecked;
            }

            try {
                await Task.Delay(fetchDelay);
                (Uri, Version, string) data = await GetLatestRelease(Constants.ApplicationType.IsTestBuild);
                int verCompare = GithubUtil.CompareAssemblyVersion(data.Item2);
                if (verCompare > 0) {
                    //update available.
                    Status = AppUpdateStatus.available;
                }
                else if (verCompare < 0) {
                    //beta release.
                    Status = AppUpdateStatus.invalid;
                }
                else {
                    //up-to-date.
                    Status = AppUpdateStatus.uptodate;
                }
                LastCheckUri = data.Item1;
                LastCheckVersion = data.Item2;
                LastCheckChangelog = data.Item3;
            }
            catch (Exception e) {
                Debug.WriteLine("Update fetch Error:" + e.ToString());
                Status = AppUpdateStatus.error;
            }
            LastCheckTime = DateTime.Now;

            UpdateChecked?.Invoke(this, new AppUpdaterEventArgs(Status, LastCheckVersion, LastCheckTime, LastCheckUri, LastCheckChangelog));
            return Status;
        }

        public async Task<(Uri, Version, string)> GetLatestRelease(bool isBeta) {
            var userName = "PaperHammer";
            var repositoryName = isBeta ? "VirtualPaper-beta" : "VirtualPaper";
            var gitRelease = await GithubUtil.GetLatestRelease(repositoryName, userName, 0);
            Version version = GithubUtil.GetVersion(gitRelease);

            //download asset format: virtualpaper_setup_x64_full_vXXXX.exe, XXXX - 4 digit version no.
            var gitUrl = await GithubUtil.GetAssetUrl("virtualpaper_setup_x64_full",
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
            if ((DateTime.Now - LastCheckTime).TotalMilliseconds > (Status != AppUpdateStatus.error ? _fetchDelayRepeat : _fetchDelayError)) {
                _ = CheckUpdate(0);
            }
        }
        #endregion

        private readonly int _fetchDelayError = 30 * 60 * 1000; //30min
        private readonly int _fetchDelayRepeat = 12 * 60 * 60 * 1000; //12hr
        private readonly Timer _retryTimer = new();
    }
}
