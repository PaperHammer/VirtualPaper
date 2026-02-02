using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using Windows.ApplicationModel.DataTransfer;
using UAC = UACHelper.UACHelper;

namespace VirtualPaper.DraftPanel.ViewModels {
    public class GetStartViewModel {
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
                    _recentUsed.Remove(item);
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
            _recentUsed = [.. RecentUseds];
        }

        internal List<IRecentUsed> _recentUsed = [];
        private readonly IUserSettingsClient _userSettingsClient;
    }
}
