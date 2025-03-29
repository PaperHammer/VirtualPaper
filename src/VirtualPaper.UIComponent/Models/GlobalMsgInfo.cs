using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Models {
    public partial class GlobalMsgInfo : ObservableObject {
        private bool _isOpen = false;
        public bool IsOpen {
            get => _isOpen;
            set { _isOpen = value; OnPropertyChanged(); }
        }

        public string Key { get; }
        public string Message { get; set; }
        public InfoBarSeverity Severity { get; set; }

        public GlobalMsgInfo(string key, bool isNeedLocallizer, string msgOrKey, InfoBarSeverity infoBarSeverity) {
            Key = key;
            Severity = infoBarSeverity;
            Message = isNeedLocallizer ? LanguageUtil.GetI18n(msgOrKey) : msgOrKey;
            IsOpen = true;
        }
    }
}
