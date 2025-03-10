using System.Threading;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.ViewModels {
    public partial class LoadingViewModel : ObservableObject {
        private bool _frameIsEnable = true;
        public bool FrameIsEnable {
            get { return _frameIsEnable; }
            set { _frameIsEnable = value; OnPropertyChanged(); }
        }

        private double _frameOpacity = 1;
        public double FrameOpacity {
            get { return _frameOpacity; }
            set { _frameOpacity = value; OnPropertyChanged(); }
        }

        private bool _loadingIsVisiable;
        public bool IsVisiable {
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

        private string _textLoading = LanguageUtil.GetI18n(Constants.I18n.Text_Loading);
        public string TextLoading {
            get { return _textLoading; }
            set { _textLoading = value; OnPropertyChanged(); }
        }

        private string _textCancel = LanguageUtil.GetI18n(Constants.I18n.Text_Cancel);
        public string TextCancel {
            get { return _textCancel; }
            set { _textCancel = value; OnPropertyChanged(); }
        }

        private CancellationTokenSource[] _ctsTokens;
        public CancellationTokenSource[] CtsTokens {
            get { return _ctsTokens; }
            set { _ctsTokens = value; OnPropertyChanged(); }
        }

        public async void Loading(
            bool cancelEnable,
            bool progressbarEnable,
            CancellationTokenSource[] cts = null) {

            if (_loadingSemaphoreSlim.CurrentCount == 0) return;

            await _loadingSemaphoreSlim.WaitAsync();

            FrameIsEnable = false;
            FrameOpacity = 0.4;
            IsVisiable = true;
            CtsTokens = cts;
            CancelEnable = cancelEnable;
            ProgressbarEnable = progressbarEnable;
        }

        public void Loaded(CancellationTokenSource[] cts = null) {
            if (cts != null) {
                foreach (var ct in cts) {
                    ct?.Dispose();
                }
            }

            IsVisiable = false;
            FrameIsEnable = true;
            FrameOpacity = 1;

            if (_loadingSemaphoreSlim.CurrentCount < 1) {
                _loadingSemaphoreSlim.Release();
            }
        }

        public void UpdateProgressbarValue(int curValue, int toltalValue) {
            CurValue = curValue;
            TotalValue = toltalValue;
        }

        private readonly SemaphoreSlim _loadingSemaphoreSlim = new(1, 1);
    }
}
