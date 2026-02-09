using System;
using System.Windows.Input;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.DraftPanel.ViewModels {
    public partial class ConfigSpaceViewModel : ObservableObject {
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

        private Action? nextStep;
        internal Action? NextStep { get => nextStep; set => nextStep = value; }
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
                NextStep.Invoke();
            });
        }
    }
}
