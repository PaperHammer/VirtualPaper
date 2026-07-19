using System.Threading.Tasks;
using VirtualPaper.Common;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Entry.FileLoaders;
using Workloads.Utils.DraftUtils.Models;

namespace Workloads.Entry.FileLoaders.Specific {
    public class DesignProjectFileLoader : IProjectFileLoader {
        public bool CanLoad(FileType fileType) => fileType == FileType.FDesign;

        public async Task<ProjectFileLoadResult?> LoadAsync(string filePath, FileType fileType) {
            var result = await StaticImgDesignFileUtil.GetFileHeaderAsync(filePath);
            if (result is not FileHeader header) {
                return null;
            }

            return header.ProjType switch {
                ProjectType.P_StaticImage => new ProjectFileLoadResult(filePath, FileType.FDesign),
                _ => null,
            };
        }
    }
}
