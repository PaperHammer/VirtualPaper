using System;
using System.Windows.Input;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Data;

namespace VirtualPaper.DraftPanel.ViewModels {
    public partial class ConfigSpaceViewModel : ObservableObject, IDisposable {
        private string? _previousStepBtnText;
        public string? PreviousStepBtnText { get => _previousStepBtnText; set { _previousStepBtnText = value; OnPropertyChanged(); } }

        private string? _nextStepBtnText;
        public string? NextStepBtnText { get => _nextStepBtnText; set { _nextStepBtnText = value; OnPropertyChanged(); } }

        private bool _isNextEnable;
        public bool IsNextEnable { get => _isNextEnable; set { _isNextEnable = value; OnPropertyChanged(); } }

        private bool _btnVisible;
        public bool BtnVisible { get => _btnVisible; set { _btnVisible = value; OnPropertyChanged(); } }

        public ICommand? PreviousStepCommand { get; private set; }
        public ICommand? NextStepCommand { get; private set; }

        public ConfigSpaceViewModel() {
            InitCommand();
        }

        private void InitCommand() {
            PreviousStepCommand = new RelayCommand(async () => {
                if (_cardComponent?.PreviousStepAction is { } action) {
                    await action(null);
                }
            });
            NextStepCommand = new RelayCommand(async () => {                
                if (_cardComponent?.NextStepAction is { } action) {
                    await action(null);
                }
            });
        }

        internal void RefreshCardComponentData() {
            if (_cardComponent == null) return;

            PreviousStepBtnText = _cardComponent.PreviousStepBtnText;
            NextStepBtnText = _cardComponent.NextStepBtnText;
            IsNextEnable = _cardComponent.IsNextEnable;
            BtnVisible = _cardComponent.BtnVisible;
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    PreviousStepCommand = null;
                    PreviousStepCommand = null;
                    NextStepCommand = null;
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        internal ICardComponent? _cardComponent;
    }
}
