using VirtualPaper.IntelligentPanel.Utils.Interfaces;

namespace VirtualPaper.IntelligentPanel.ViewModels {
    public class IntelligentViewModel {
        public IIntelligentPage? SelectedIntelliPage { get; internal set; }

        public IntelligentViewModel() {
        }

        internal void AddTask() {
            SelectedIntelliPage?.AddTask();
        }
    }
}
