using System;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.IntelligentPanel.Utils.Interfaces;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.IntelligentPanel.ViewModels {
    public class IntelligentViewModel {
        public IIntelligentPage? SelectedIntelliPage { get; internal set; }
        public Action? CardUIStateChanged { get; set; }
        public string PreviousStepBtnText { get; private set; } = string.Empty;
        public string NextStepBtnText { get; private set; } = string.Empty;
        public bool BtnVisible { get; private set; } = false;

        private bool _isNextEnable;
        public bool IsNextEnable {
            get { return _isNextEnable; }
            set { _isNextEnable = value; CardUIStateChanged?.Invoke(); }
        }

        public IntelligentViewModel() {
        }

        public void UpdateCardComponentUI() {
            PreviousStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel));
            NextStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Confirm));

            BtnVisible = true;
            CardUIStateChanged?.Invoke();
        }

        public async Task OnNextStepClickedAsync() {
            // todo
            _intelligentTCS?.TrySetResult(null);
        }

        public async Task OnPreviousStepClickedAsync() {
            _intelligentTCS?.TrySetResult(null);
        }

        internal void AddTask(string[]? paths) {
            SelectedIntelliPage?.AddTask(paths);
        }

        internal TaskCompletionSource<string[]?>? _intelligentTCS;
    }
}
