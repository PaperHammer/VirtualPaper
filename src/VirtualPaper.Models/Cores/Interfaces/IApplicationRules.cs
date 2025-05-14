using VirtualPaper.Common;

namespace VirtualPaper.Models.Cores.Interfaces {
    public interface IApplicationRules {
        string AppName { get; set; }
        AppWpRunRulesEnum Rule { get; set; }
    }
}
