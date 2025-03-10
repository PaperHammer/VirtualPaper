using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;

namespace VirtualPaper.DraftPanel.Model {
    [JsonSerializable(typeof(ProjectMetadata))]
    public partial class ProjectMetadataContext : JsonSerializerContext { }

    public class ProjectMetadata {
        public string Name { get; init; }
        public ProjectType Type { get; init; }

        public ProjectMetadata() { }

        public ProjectMetadata(string name, ProjectType projectType) {
            Name = name;
            Type = projectType;
        }

        internal async Task WriteDataAsync(string storageFolderPath) {
            string storageFolder = Path.Combine(storageFolderPath, Name);
            Directory.CreateDirectory(storageFolder);
            await JsonStorage.StoreAsync(
                Path.Combine(storageFolder, Name + ".vproj"),
                this,
                ProjectMetadataContext.Default);
        }
    }
}
