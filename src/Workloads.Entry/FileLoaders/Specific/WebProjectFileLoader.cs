using System.Threading.Tasks;
using VirtualPaper.Common;

namespace Workloads.Entry.FileLoaders.Specific {
    public class WebProjectFileLoader : IProjectFileLoader {
        public bool CanLoad(FileType fileType) => fileType == FileType.FWebDesign;

        public Task<ProjectFileLoadResult?> LoadAsync(string filePath, FileType fileType) {
            return Task.FromResult<ProjectFileLoadResult?>(new ProjectFileLoadResult(filePath, fileType));
        }
    }
}
