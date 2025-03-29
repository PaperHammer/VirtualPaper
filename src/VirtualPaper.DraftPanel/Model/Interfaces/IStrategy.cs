using System.Threading.Tasks;
using VirtualPaper.Common;

namespace VirtualPaper.DraftPanel.Model.Interfaces {
    interface IStrategy {
        bool CanHandle(ConfigSpacePanelType startupType);
        void Handle(IConfigSpace projectBridge);
        Task HandleAsync(IConfigSpace projectBridge);
    }
}
