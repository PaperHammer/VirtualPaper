using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using Windows.ApplicationModel.DataTransfer;
using UAC = UACHelper.UACHelper;

namespace VirtualPaper.DraftPanel.ViewModels {
    public partial class GetStartViewModel {
        public ObservableCollection<IRecentUsed> RecentUseds { get; private set; } = [];
        public ICommand? RemoveFromListCommand { get; private set; }
        public ICommand? CopyPathCommand { get; private set; }
        public bool IsElevated { get; }

        public GetStartViewModel(IUserSettingsClient userSettingsClient) {
            IsElevated = UAC.IsElevated;

            this._userSettingsClient = userSettingsClient;
            InitCollection();
            InitCommand();
        }

        private void InitCommand() {
            RemoveFromListCommand = new RelayCommand<IRecentUsed>(async item => {
                if (item != null) {
                    RecentUseds.Remove(item);
                    _recentUseds?.Remove(item);
                    await _userSettingsClient.DeleteRecetUsedAsync(item);
                }
            });

            CopyPathCommand = new RelayCommand<IRecentUsed>(item => {
                if (item?.FilePath != null) {
                    var package = new DataPackage();
                    package.SetText(item.FilePath);
                    Clipboard.SetContent(package);
                }
            });
        }

        private void InitCollection() {
            RecentUseds.AddRange(_userSettingsClient.RecentUseds);
            _recentUseds = [.. RecentUseds];
        }

        #region filter
        internal void ApplyFilter(string keyword) {
            FilterByTitle(keyword);
        }

        internal void FilterByTitle(string keyword) {
            var filtered = _recentUseds?.Where(recentUsed =>
                recentUsed.FileName != null && recentUsed.FileName.Contains(keyword, StringComparison.InvariantCultureIgnoreCase)
            );
            if (filtered == null) return;
            Remove_NonMatching(filtered);
            AddBack_Procs(filtered);
        }

        private void Remove_NonMatching(IEnumerable<IRecentUsed> recentuseds) {
            for (int i = RecentUseds.Count - 1; i >= 0; i--) {
                var item = RecentUseds[i];
                if (!recentuseds.Contains(item)) {
                    RecentUseds.Remove(item);
                }
            }
        }

        private void AddBack_Procs(IEnumerable<IRecentUsed> recentuseds) {
            foreach (var item in recentuseds) {
                if (!RecentUseds.Contains(item)) {
                    RecentUseds.Add(item);
                }
            }
        }
        #endregion

        private List<IRecentUsed>? _recentUseds;
        private readonly IUserSettingsClient _userSettingsClient;
    }
}
