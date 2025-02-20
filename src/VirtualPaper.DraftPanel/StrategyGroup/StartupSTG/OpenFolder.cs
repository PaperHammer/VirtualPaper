using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.Bridge;

namespace VirtualPaper.DraftPanel.StrategyGroup.StartupSTG {
    internal class OpenFolder : IStrategy {
        public bool CanHandle(DraftPanelStartupType type) {
            return type == DraftPanelStartupType.OpenFolder;
        }
        public void Handle(IDraftPanelBridge projectBridge) {
            HandleAsync(projectBridge).GetAwaiter().GetResult();
        }

        public async Task HandleAsync(IDraftPanelBridge projectBridge) {
            var storageFolder = await WindowsStoragePickers.PickFolderAsync(projectBridge.GetWindowHandle());
            if (storageFolder == null) return;
            projectBridge.ChangePanelState(DraftPanelState.WorkSpace, storageFolder.Path);
        }
    }
}
