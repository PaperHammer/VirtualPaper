using System.ComponentModel;
using System.Globalization;
using System.Resources;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.lang {
    public partial class LanguageManager : ObservableObject {
        public static LanguageManager Instance => _lazy.Value;

        public LanguageManager() {
            _resourceManager = new ResourceManager("VirtualPaper.Properties.lang", typeof(LanguageManager).Assembly);
        }

        public string this[string name] {
            get => _resourceManager.GetString(name) ?? "";
        }

        public void ChangeLanguage(CultureInfo cultureInfo) {
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
            OnPropertyChanged("Item[]");
        }

        private readonly ResourceManager _resourceManager;
        private static readonly Lazy<LanguageManager> _lazy = new(() => new LanguageManager());
    }
}
