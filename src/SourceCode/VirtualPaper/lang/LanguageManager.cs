using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace VirtualPaper.lang
{
    public class LanguageManager : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public static LanguageManager Instance => _lazy.Value;

        public LanguageManager()
        {
            _resourceManager = new ResourceManager("VirtualPaper.Properties.lang", typeof(LanguageManager).Assembly);
        }

        public string this[string name]
        {
            get =>_resourceManager.GetString(name) ?? "";
        }

        public void ChangeLanguage(CultureInfo cultureInfo)
        {
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("item[]"));
        }

        private readonly ResourceManager _resourceManager;
        private static readonly Lazy<LanguageManager> _lazy = new(() => new LanguageManager());
    }
}
