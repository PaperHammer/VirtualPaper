using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.Bridge;

namespace VirtualPaper.DraftPanel.StrategyGroup.StartupSTG {
    internal class OpenVpd : IStrategy {
        public bool CanHandle(DraftPanelStartupType type) {
            return type == DraftPanelStartupType.OpenVpd;
        }
        public void Handle(IDraftPanelBridge projectBridge) {
            HandleAsync(projectBridge).GetAwaiter().GetResult();
        }

        public async Task HandleAsync(IDraftPanelBridge projectBridge) {
            var storage = await WindowsStoragePickers.PickFilesAsync(projectBridge.GetWindowHandle(), FileFilter.FileExtensions[FileType.FDesign]);
            if (storage.Length < 1) return;
            var filePath = storage[0].Path;
            projectBridge.ChangePanelState(DraftPanelState.WorkSpace, new List<string>() { filePath });
        }
    }
}
