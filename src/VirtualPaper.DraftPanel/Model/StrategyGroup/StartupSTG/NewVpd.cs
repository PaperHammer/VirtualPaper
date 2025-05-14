using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.DraftPanel.Model.Interfaces;

namespace VirtualPaper.DraftPanel.Model.StrategyGroup.StartupSTG {
    internal class NewVpd : IStrategy {
        public bool CanHandle(ConfigSpacePanelType type) {
            return type == ConfigSpacePanelType.NewVpd;
        }

        public void Handle(IDraftPanelBridge configBridge) {
            HandleAsync(configBridge).GetAwaiter().GetResult();
        }

        public async Task HandleAsync(IDraftPanelBridge configBridge) {
            await Task.Run(() => {
                configBridge.ChangePanelState(DraftPanelState.ProjectConfig, null);
            });
        }
    }
}
