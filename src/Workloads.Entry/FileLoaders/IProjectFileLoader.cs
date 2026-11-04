using System.Threading.Tasks;
using VirtualPaper.Common;

namespace Workloads.Entry.FileLoaders {
    public interface IProjectFileLoader {
        bool CanLoad(FileType fileType);
        Task<ProjectFileLoadResult?> LoadAsync(string filePath, FileType fileType);
    }
}
