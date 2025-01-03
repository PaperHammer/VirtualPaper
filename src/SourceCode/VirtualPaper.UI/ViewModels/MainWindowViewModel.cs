﻿using System.Threading;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.UI.ViewModels {
    public partial class MainWindowViewModel : ObservableObject {
        public string SidebarGallery { get; private set; }
        public string SidebarWpSettings { get; private set; }
        public string SidebarProject { get; private set; }
        public string SidebarAccount { get; private set; }
        public string SidebarAppSettings { get; private set; }

        #region loading
        private bool _frameIsEnable;
        public bool FrameIsEnable {
            get { return _frameIsEnable; }
            set { _frameIsEnable = value; OnPropertyChanged(); }
        }

        private bool _loadingIsVisiable;
        public bool LoadingIsVisiable {
            get { return _loadingIsVisiable; }
            set { _loadingIsVisiable = value; OnPropertyChanged(); }
        }

        private int _curValue;
        public int CurValue {
            get { return _curValue; }
            set { _curValue = value; OnPropertyChanged(); }
        }

        private int _totalValue;
        public int TotalValue {
            get { return _totalValue; }
            set { _totalValue = value; OnPropertyChanged(); }
        }

        private bool _cancelEanble;
        public bool CancelEnable {
            get { return _cancelEanble; }
            set { _cancelEanble = value; OnPropertyChanged(); }
        }

        private bool _progressbarEnable;
        public bool ProgressbarEnable {
            get { return _progressbarEnable; }
            set { _progressbarEnable = value; OnPropertyChanged(); }
        }

        private string _textLoading;
        public string TextLoading {
            get { return _textLoading; }
            set { _textLoading = value; OnPropertyChanged(); }
        }

        private string _textCancel;
        public string TextCancel {
            get { return _textCancel; }
            set { _textCancel = value; OnPropertyChanged(); }
        }

        private CancellationTokenSource[] _ctsTokens;
        public CancellationTokenSource[] CtsTokens {
            get { return _ctsTokens; }
            set { _ctsTokens = value; OnPropertyChanged(); }
        }
        #endregion

        #region infobar
        private bool _infoBarIsOpen = false;
        public bool InfoBarIsOpen {
            get => _infoBarIsOpen;
            set { _infoBarIsOpen = value; OnPropertyChanged(); }
        }

        private string _infobarMsg;
        public string InfobarMsg {
            get { return _infobarMsg; }
            set { _infobarMsg = value; OnPropertyChanged(); }
        }

        private InfoBarSeverity _infoBarSeverity;
        public InfoBarSeverity InfoBarSeverity {
            get { return _infoBarSeverity; }
            set { _infoBarSeverity = value; OnPropertyChanged(); }
        }
        #endregion

        public MainWindowViewModel() {
            InitText();
        }

        private void InitText() {
            SidebarGallery = App.GetI18n(Constants.I18n.SidebarGallery);
            SidebarWpSettings = App.GetI18n(Constants.I18n.SidebarWpSettings);
            SidebarProject = App.GetI18n(Constants.I18n.SidebarProject);
            SidebarAccount = App.GetI18n(Constants.I18n.SidebarAccount);
            SidebarAppSettings = App.GetI18n(Constants.I18n.SidebarAppSettings);
            TextLoading = App.GetI18n(Constants.I18n.Text_Loading);
            TextCancel = App.GetI18n(Constants.I18n.Text_Cancel);
        }

        #region loading_ui_logic
        internal async void Loading(
            bool cancelEnable,
            bool progressbarEnable,
            CancellationTokenSource[] cts) {

            if (_loadingSemaphoreSlim.CurrentCount == 0) return;

            await _loadingSemaphoreSlim.WaitAsync();

            FrameIsEnable = false;
            LoadingIsVisiable = true;
            CtsTokens = cts;
            CancelEnable = cancelEnable;
            ProgressbarEnable = progressbarEnable;
        }

        internal void Loaded(CancellationTokenSource[] cts) {
            if (cts != null) {
                foreach (var ct in cts) {
                    ct?.Dispose();
                }
            }

            LoadingIsVisiable = false;
            FrameIsEnable = true;

            if (_loadingSemaphoreSlim.CurrentCount < 1) {
                _loadingSemaphoreSlim.Release();
            }
        }

        internal void UpdateProgressbarValue(int curValue, int toltalValue) {
            CurValue = curValue;
            TotalValue = toltalValue;
        }
        #endregion

        #region infobar_logic
        internal void ShowMessge(
            bool isNeedLocallizer,
            string msg,
            InfoBarSeverity infoBarSeverity) {
            InfoBarSeverity = infoBarSeverity;
            InfobarMsg = isNeedLocallizer ? App.GetI18n(msg) : msg;
            InfoBarIsOpen = true;
        }
        #endregion

        private readonly SemaphoreSlim _loadingSemaphoreSlim = new(1, 1);
    }
}
