using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;

namespace VirtualPaper.DraftPanel.StrategyGroup.StartupSTG {
    internal class NewVpd : IStrategy {
        public bool CanHandle(DraftPanelStartupType type) {
            return type == DraftPanelStartupType.NewVpd;
        }

        public void Handle(IDraftPanelBridge projectBridge) {
            HandleAsync(projectBridge).GetAwaiter().GetResult();
        }

        public async Task HandleAsync(IDraftPanelBridge projectBridge) {
            await Task.Run(() => {
                projectBridge.ChangeProjectPanelState(DraftPanelState.ProjectConfig);
            });
        }
    }
}
