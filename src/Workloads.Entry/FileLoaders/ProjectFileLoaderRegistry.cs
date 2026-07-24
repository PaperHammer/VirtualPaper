using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.UIComponent.Utils;

namespace Workloads.Entry.FileLoaders {
    public class ProjectFileLoaderRegistry {
        public ProjectFileLoaderRegistry(IEnumerable<IProjectFileLoader> loaders) {
            _loaders = loaders.ToArray();
        }

        public async Task<ProjectFileLoadResult?> LoadAsync(string filePath) {
            var fileType = FileFilter.GetRuntimeFileType(System.IO.Path.GetExtension(filePath));
            var loader = _loaders.FirstOrDefault(x => x.CanLoad(fileType));
            if (loader == null) {
                GlobalMessageUtil.ShowError(
                    message: nameof(Constants.I18n.Project_FileLoad_Failed),
                    isNeedLocalizer: true,
                    extraMsg: filePath);
                return null;
            }

            var result = await loader.LoadAsync(filePath, fileType);
            if (result == null) {
                GlobalMessageUtil.ShowError(
                    message: nameof(Constants.I18n.Project_FileLoad_Failed),
                    isNeedLocalizer: true,
                    extraMsg: filePath);
            }
            return result;
        }
        
        private readonly IReadOnlyList<IProjectFileLoader> _loaders;
    }
}
