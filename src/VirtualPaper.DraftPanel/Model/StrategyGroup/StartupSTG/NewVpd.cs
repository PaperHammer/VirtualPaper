using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.DraftPanel.Model.Interfaces;

namespace VirtualPaper.DraftPanel.Model.StrategyGroup.StartupSTG {
    internal class NewVpd : IStrategy {
        public bool CanHandle(DraftPanelStartupType type) {
            return type == DraftPanelStartupType.NewVpd;
        }

        public void Handle(IDraftPanelBridge projectBridge) {
            HandleAsync(projectBridge).GetAwaiter().GetResult();
        }

        public async Task HandleAsync(IDraftPanelBridge projectBridge) {
            await Task.Run(() => {
                projectBridge.ChangePanelState(DraftPanelState.ProjectConfig);
            });
        }
    }
}
