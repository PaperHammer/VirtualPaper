using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.Utils.Interfaces;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.IntelligentPanel.ViewModels {
    public class IntelligentViewModel {
        public IIntelligentPage? SelectedIntelliPage { get; internal set; }        

        public IntelligentViewModel() {
        }        

        internal bool AddTask(IIntelliData? data) {
            if (SelectedIntelliPage == null || data == null) return false;

            bool res = SelectedIntelliPage.AddTask(data);            
            
            return res;
        }
    }
}
