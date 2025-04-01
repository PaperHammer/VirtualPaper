using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Model.NavParam;

namespace VirtualPaper.DraftPanel.Model.StrategyGroup.StartupSTG {
    internal class OpenFile : IStrategy {
        public bool CanHandle(ConfigSpacePanelType type) {
            return type == ConfigSpacePanelType.OpenFile;
        }
        public void Handle(IConfigSpace configBridge) {
            HandleAsync(configBridge).GetAwaiter().GetResult();
        }

        public async Task HandleAsync(IConfigSpace configBridge) {
            var storage = await WindowsStoragePickers.PickFilesAsync(configBridge.GetWindowHandle(), FileFilter.FileTypeToExtension[FileType.FImage], true);
            if (storage.Length < 1) return;

            int n = storage.Length;
            string[] filePaths = new string[n];
            for (int i = 0; i < storage.Length; i++) {
                filePaths[i] = storage[i].Path;
            }
            configBridge.ChangePanelState(DraftPanelState.WorkSpace, new ToWorkSpace([.. filePaths]));
        }
    }
}
