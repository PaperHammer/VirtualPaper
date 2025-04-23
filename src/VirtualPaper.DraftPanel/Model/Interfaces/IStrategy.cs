using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;

namespace VirtualPaper.DraftPanel.Model.Interfaces {
    interface IStrategy {
        bool CanHandle(ConfigSpacePanelType startupType);
        void Handle(IDraftPanelBridge projectBridge);
        Task HandleAsync(IDraftPanelBridge projectBridge);
    }
}
