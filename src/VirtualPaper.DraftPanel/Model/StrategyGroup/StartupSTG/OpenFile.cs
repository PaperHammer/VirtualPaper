using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model.Interfaces;

namespace VirtualPaper.DraftPanel.Model.StrategyGroup.StartupSTG {
    internal class OpenFile : IStrategy {
        public bool CanHandle(DraftPanelStartupType type) {
            return type == DraftPanelStartupType.OpenFile;
        }
        public void Handle(IDraftPanelBridge projectBridge) {
            HandleAsync(projectBridge).GetAwaiter().GetResult();
        }

        public async Task HandleAsync(IDraftPanelBridge projectBridge) {
            var storage = await WindowsStoragePickers.PickFilesAsync(projectBridge.GetWindowHandle(), FileFilter.FileExtensions[FileType.FImage], true);
            if (storage.Length < 1) return;
            List<string> filePaths = [];
            foreach (var sg in storage) {
                filePaths.Add(sg.Path);
            }
            projectBridge.ChangePanelState(DraftPanelState.WorkSpace, filePaths);
        }
    }
}
