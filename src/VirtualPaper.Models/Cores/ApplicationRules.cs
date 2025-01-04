using System.Text.Json.Serialization;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.Models.Cores {
    [JsonSerializable(typeof(ApplicationRules))]
    [JsonSerializable(typeof(IApplicationRules))]
    [JsonSerializable(typeof(List<ApplicationRules>))]
    [JsonSerializable(typeof(List<IApplicationRules>))]
    public partial class ApplicationRulesContext : JsonSerializerContext { }

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
