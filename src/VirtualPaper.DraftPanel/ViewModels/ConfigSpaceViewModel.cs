using Microsoft.UI.Xaml;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.DraftPanel.ViewModels {
    internal partial class ConfigSpaceViewModel : ObservableObject {
        private string _previousStepBtnText;
        public string PreviousStepBtnText { get => _previousStepBtnText; set { _previousStepBtnText = value; OnPropertyChanged(); } }

        private string _nextStepBtnText;
        public string NextStepBtnText { get => _nextStepBtnText; set { _nextStepBtnText = value; OnPropertyChanged(); } }

        private bool _isNextEnable;
        public bool IsNextEnable { get => _isNextEnable; set { _isNextEnable = value; OnPropertyChanged(); } }

        private bool _btnVisible;
        public bool BtnVisible { get => _btnVisible; set { _btnVisible = value; OnPropertyChanged(); } }

        private RoutedEventHandler previousStep;
        internal RoutedEventHandler PreviousStep { get => previousStep; set => previousStep = value; }

        private RoutedEventHandler nextStep;
        internal RoutedEventHandler NextStep { get => nextStep; set => nextStep = value; }
    }
}
