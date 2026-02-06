using System;
using Microsoft.UI.Xaml;
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

        private RoutedEventHandler? previousStep;
        internal RoutedEventHandler? PreviousStep { get => previousStep; set => previousStep = value; }

        private RoutedEventHandler? nextStep;
        internal RoutedEventHandler? NextStep { get => nextStep; set => nextStep = value; }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    PreviousStepBtnText = null;
                    NextStepBtnText = null;
                    PreviousStep = null;
                    NextStep = null;
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
