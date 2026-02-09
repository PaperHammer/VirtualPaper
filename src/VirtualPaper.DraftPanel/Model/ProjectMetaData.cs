using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;

namespace VirtualPaper.DraftPanel.Model {
    [JsonSerializable(typeof(ProjectMetaData))]
    [JsonSerializable(typeof(ProjectTag))]
    [JsonSerializable(typeof(List<ProjectTag>))]
    public partial class DraftMetadataContext : JsonSerializerContext { }

    public class ProjectMetaData {
        public string Name { get; } = null!;
        public string DraftVersion { get; } = null!;
        public List<ProjectTag> ProjectTags { get; } = null!;

        [JsonConstructor]
        [Obsolete("This constructor is intended for JSON deserialization only. Use the another ctor instead.")]
        public ProjectMetaData(string name, string draftVersion, List<ProjectTag> projectTags) {
            Name = name;
            DraftVersion = draftVersion;
            ProjectTags = projectTags;
        }

        public ProjectMetaData(string draftName, PreProjectData preData) {
            Name = draftName;
            DraftVersion = Constants.CoreField.DraftFileVersion;
            ProjectTags = [
                new ProjectTag(preData.ProjName, preData.Type),
            ];
        }

        internal async Task SaveAsync(string storageFolderPath) {
            await JsonSaver.SaveAsync(
                Path.Combine(storageFolderPath, Name + FileExtension.FE_Design),
                this,
                DraftMetadataContext.Default);
        }

        internal static async Task<ProjectMetaData> LoadAsync(string filePath) {
            return await JsonSaver.LoadAsync<ProjectMetaData>(filePath, DraftMetadataContext.Default);
        }
    }

    public record ProjectTag {
        public string Name { get; set; }
        public ProjectType Type { get; set; }
        public string EntryRelativeFilePath { get; set; }
        public int NameHash { get; set; }        

        public ProjectTag(string? name, ProjectType type) {
            ArgumentNullException.ThrowIfNull(name);

            Name = name;
            Type = type;
            EntryRelativeFilePath = $"{name}/{name}{FileExtension.FE_Design}";
            NameHash = IdentifyUtil.ComputeHash(name);
        }
    }

    public record PreProjectData {
        public string? ProjName { get; }
        public string[]? FilePaths { get; }
        public ProjectType Type { get; }

        public PreProjectData(string projName, ProjectType type) {
            ProjName = projName;
            Type = type;
        }
        
        public PreProjectData(string[] filePaths, ProjectType type) {
            FilePaths = filePaths;
            Type = type;
        }
    }
}
