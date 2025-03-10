using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.ViewModels {
    public class GlobalMsgViewModel : ObservableObject {
        private bool _infoBarIsOpen = false;
        public bool InfoBarIsOpen {
            get => _infoBarIsOpen;
            set { _infoBarIsOpen = value; OnPropertyChanged(); }
        }

        private string _infobarMsg;
        public string InfobarMsg {
            get { return _infobarMsg; }
            set { _infobarMsg = value; OnPropertyChanged(); }
        }

        private InfoBarSeverity _infoBarSeverity;
        public InfoBarSeverity InfoBarSeverity {
            get { return _infoBarSeverity; }
            set { _infoBarSeverity = value; OnPropertyChanged(); }
        }

        public void ShowMessge(
            bool isNeedLocallizer,
            string msg,
            InfoBarSeverity infoBarSeverity) {
            InfoBarSeverity = infoBarSeverity;
            InfobarMsg = isNeedLocallizer ? LanguageUtil.GetI18n(msg) : msg;
            InfoBarIsOpen = true;
        }
    }
}
