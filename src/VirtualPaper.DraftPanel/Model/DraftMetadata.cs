using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model.NavParam;

namespace VirtualPaper.DraftPanel.Model {
    [JsonSerializable(typeof(DraftMetadata))]
    [JsonSerializable(typeof(ProjectTag))]
    [JsonSerializable(typeof(List<ProjectTag>))]
    internal partial class DraftMetadataContext : JsonSerializerContext { }

    internal class DraftMetadata {
        public string Name { get; init; } = string.Empty;
        public Version DraftVersion { get; init; }
        public List<ProjectTag> ProjectTags { get; init; } = [];

        [JsonConstructor]
        [Obsolete("This constructor is intended for JSON deserialization only. Use the another method instead.")]
        public DraftMetadata(string name, Version draftVersion, List<ProjectTag> projectTags) {
            Name = name;
            DraftVersion = draftVersion;
            ProjectTags = projectTags;
        }

        public DraftMetadata(string draftName, ToDraftConfig data) {
            Name = draftName;
            DraftVersion = Assembly.GetEntryAssembly().GetName().Version;
            ProjectTags = [
                new(data.ProjName, data.ProjType),
            ];
        }

        internal async Task SaveAsync(string storageFolderPath) {
            await JsonSaver.SaveAsync(
                Path.Combine(storageFolderPath, Name + FileExtension.FE_Design),
                this,
                DraftMetadataContext.Default);
        }

        internal static async Task<DraftMetadata> LoadAsync(string filePath) {
            return await JsonSaver.LoadAsync<DraftMetadata>(filePath, DraftMetadataContext.Default);
        }
    }

    public record ProjectTag {
        public string Name { get; set; }
        public ProjectType Type { get; set; }
        public string EntryRelativeFilePath { get; set; }
        public int NameHash { get; set; }        

        public ProjectTag(string name, ProjectType type) {
            Name = name;
            Type = type;
            EntryRelativeFilePath = $"{name}/{name}{FileExtension.FE_Project}";
            NameHash = IdentifyUtil.ComputeHash(name);            
        }
    }
}
