using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using VirtualPaper.Common;
using VirtualPaper.EditPanel.Model;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using Windows.ApplicationModel.DataTransfer;
using UAC = UACHelper.UACHelper;

namespace VirtualPaper.EditPanel.ViewModels {
    public partial class GetStartViewModel {
        public Action? CardUIStateChanged { get; set; }
        public ObservableCollection<IRecentUsed> RecentUseds { get; private set; } = [];
        public ICommand? RemoveFromListCommand { get; private set; }
        public ICommand? CopyPathCommand { get; private set; }
        public ICommand? ShowOnDiskCommand { get; private set; }
        public bool IsElevated { get; }
        public bool BtnVisible { get; private set; } = false;
        public string PreviousStepBtnText { get; private set; } = string.Empty;
        public TaskCompletionSource<PreProjectData[]?>? EditConfigTCS { get; set; }
        public bool IsFromWorkSpaceForAddProj { get; set; }

        public GetStartViewModel(IUserSettingsClient userSettingsClient) {
            IsElevated = UAC.IsElevated;

            this._userSettingsClient = userSettingsClient;
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

            ShowOnDiskCommand = new RelayCommand<IRecentUsed>(item => {
                if (item?.FilePath != null) {
                    Process.Start("Explorer", "/select," + item.FilePath);
                }
            });
        }

        public void InitCollection() {
            RecentUseds.Clear();
            RecentUseds.AddRange(_userSettingsClient.RecentUseds);
            _recentUseds = [.. RecentUseds];
        }

        internal void UpdateCardComponentUI() {
            BtnVisible = IsFromWorkSpaceForAddProj;
            PreviousStepBtnText = BtnVisible
                ? LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel))
                : string.Empty;
            CardUIStateChanged?.Invoke();
        }

        public Task OnPreviousStepClickedAsync() {
            if (IsFromWorkSpaceForAddProj) {
                EditConfigTCS?.TrySetResult(null);
            }

            return Task.CompletedTask;
        }

        #region filter
        public void ApplyFilter(string keyword) {
            FilterByTitle(keyword);
        }

        public void FilterByTitle(string keyword) {
            var filtered = _recentUseds?.Where(recentUsed =>
                recentUsed.FileName != null && recentUsed.FileName.Contains(keyword, StringComparison.InvariantCultureIgnoreCase)
            ).ToList();
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
