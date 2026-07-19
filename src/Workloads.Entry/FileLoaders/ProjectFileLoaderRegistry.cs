using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;

namespace Workloads.Entry.FileLoaders {
    public class ProjectFileLoaderRegistry {
        private readonly IReadOnlyList<IProjectFileLoader> _loaders;

        public ProjectFileLoaderRegistry(IEnumerable<IProjectFileLoader> loaders) {
            _loaders = loaders.ToArray();
        }

        public async Task<ProjectFileLoadResult?> LoadAsync(string filePath) {
            var fileType = FileFilter.GetRuntimeFileType(System.IO.Path.GetExtension(filePath));
            var loader = _loaders.FirstOrDefault(x => x.CanLoad(fileType));
            return loader == null
                ? null
                : await loader.LoadAsync(filePath, fileType);
        }
    }
}
