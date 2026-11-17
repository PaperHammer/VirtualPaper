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
                    _lastFilterKeyword = null;
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
            _lastFilterKeyword = null;
        }

        internal void UpdateCardComponentUI() {
            BtnVisible = IsFromWorkSpaceForAddProj;
            PreviousStepBtnText = BtnVisible
                ? LanguageUtil.GetI18n(Constants.I18n.Text_Cancel)
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
            if (_recentUseds == null) return;
            if (_lastFilterKeyword != null && string.Equals(_lastFilterKeyword, keyword, StringComparison.InvariantCultureIgnoreCase)) return;
            _lastFilterKeyword = null;
            int index = 0;
            // Filtering keeps visible items in source order; only changed items need collection notifications.
            foreach (var item in _recentUseds) {
                bool visible = index < RecentUseds.Count && ReferenceEquals(RecentUseds[index], item);
                if (item.FileName?.Contains(keyword, StringComparison.InvariantCultureIgnoreCase) == true) {
                    if (!visible) RecentUseds.Insert(index, item);
                    index++;
                }
                else if (visible) {
                    RecentUseds.RemoveAt(index);
                }
            }
            while (RecentUseds.Count > index) RecentUseds.RemoveAt(RecentUseds.Count - 1);
            _lastFilterKeyword = keyword;
        }
        #endregion

        private List<IRecentUsed>? _recentUseds;
        private string? _lastFilterKeyword;
        private readonly IUserSettingsClient _userSettingsClient;
    }
}
