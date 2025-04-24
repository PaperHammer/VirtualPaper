using System.Threading;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.PlayerWeb.ViewModel {
    internal partial class MainWindowViewModel : ObservableObject {
        #region loading
        private bool _frameIsEnable;
        public bool FrameIsEnable {
            get { return _frameIsEnable; }
            set { _frameIsEnable = value; OnPropertyChanged(); }
        }

        private bool _loadingIsVisiable = true;
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

        public MainWindowViewModel() {
            InitText();
        }

        private void InitText() {           
            TextLoading = LanguageUtil.GetI18n(Constants.I18n.Text_Loading);
            TextCancel = LanguageUtil.GetI18n(Constants.I18n.Text_Cancel);
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
            TotalValue = toltalValue;
            CurValue = curValue;
        }
        #endregion

        private readonly SemaphoreSlim _loadingSemaphoreSlim = new(1, 1);
    }
}
