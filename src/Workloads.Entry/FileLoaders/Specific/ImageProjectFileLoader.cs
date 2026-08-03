using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.UIComponent.Utils;

namespace Workloads.Entry.FileLoaders.Specific {
    public class ImageProjectFileLoader : IProjectFileLoader {
        public bool CanLoad(FileType fileType) => fileType == FileType.FImage;

        public Task<ProjectFileLoadResult?> LoadAsync(string filePath, FileType fileType) {
            var detectedType = FileFilter.GetFileType(filePath);
            if (detectedType != fileType) {
                GlobalMessageUtil.ShowError(
                    message: nameof(Constants.I18n.Project_SI_FileTypeMismatch),
                    isNeedLocalizer: true,
                    extraMsg: filePath);
                return Task.FromResult<ProjectFileLoadResult?>(null);
            }

            return Task.FromResult<ProjectFileLoadResult?>(new ProjectFileLoadResult(filePath, fileType));
        }
    }
}
