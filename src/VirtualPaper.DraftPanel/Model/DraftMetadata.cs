using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.Storage;

namespace VirtualPaper.DraftPanel.Model {
    [JsonSerializable(typeof(DraftMetadata))]
    public partial class DraftMetadataContext : JsonSerializerContext { }

    public class DraftMetadata {
        public string Name { get; } = string.Empty;
        public Version DraftVersion { get; }
        public List<ProjectMetadata> Projects { get; } = [];

        public DraftMetadata() { }

        public DraftMetadata(string draftName, Version version, List<ProjectMetadata> projectMetadatas) {
            Name = draftName;
            DraftVersion = version;
            Projects = projectMetadatas;
        }

        internal async Task WriteDataAsync(string storageFolderPath) {
            await JsonStorage<DraftMetadata>.StoreDataAsync(
                Path.Combine(storageFolderPath, Name + ".vpd"),
                this,
                DraftMetadataContext.Default);

            foreach (var item in Projects) {
                await item.WriteDataAsync(storageFolderPath);
            }
        }
    }
}
