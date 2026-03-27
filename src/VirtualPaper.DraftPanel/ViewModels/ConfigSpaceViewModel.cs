using System;
using System.Windows.Input;
using VirtualPaper.Models.Mvvm;

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

        private Action? previousStep;
        internal Action? PreviousStep { get => previousStep; set => previousStep = value; }

        private Action<object?>? nextStep;
        internal Action<object?>? NextStep { get => nextStep; set => nextStep = value; }
        public ICommand? PreviousStepCommand { get; private set; }
        public ICommand? NextStepCommand { get; private set; }

        public ConfigSpaceViewModel() {
            InitCommand();
        }

        private void InitCommand() {
            PreviousStepCommand = new RelayCommand(() => {
                PreviousStep?.Invoke();
            });
            NextStepCommand = new RelayCommand(() => {
                NextStep?.Invoke(null);
            });
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    PreviousStepCommand = null;
                    NextStep = null;
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
    }
}
