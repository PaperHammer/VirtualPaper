using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Model.NavParam;

namespace VirtualPaper.DraftPanel.Model.StrategyGroup.StartupSTG {
    internal class OpenVpd : IStrategy {
        public bool CanHandle(ConfigSpacePanelType type) {
            return type == ConfigSpacePanelType.OpenVpd;
        }

        public void Handle(IDraftPanelBridge configBridge) {
            HandleAsync(configBridge).GetAwaiter().GetResult();
        }

        public async Task HandleAsync(IDraftPanelBridge configBridge) {
            var storage = await WindowsStoragePickers.PickFilesAsync(configBridge.GetWindowHandle(), FileFilter.FileTypeToExtension[FileType.FDesign]);
            if (storage.Length < 1) return;

            configBridge.ChangePanelState(DraftPanelState.WorkSpace, new ToWorkSpace([storage[0].Path]));
        }
    }
}
