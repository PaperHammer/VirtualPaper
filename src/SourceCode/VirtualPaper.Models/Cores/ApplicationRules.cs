using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.Models.Cores {
    [Serializable]
    public partial class ApplicationRules : ObservableObject, IApplicationRules {
        public ApplicationRules(string appName, AppWpRunRulesEnum rule) {
            AppName = appName;
            Rule = rule;
        }

        private string _appName = string.Empty;
        public string AppName {
            get => _appName;
            set { _appName = value; OnPropertyChanged(); }
        }

        private AppWpRunRulesEnum _rule;
        public AppWpRunRulesEnum Rule {
            get => _rule;
            set { _rule = value; OnPropertyChanged(); }
        }
    }
}
