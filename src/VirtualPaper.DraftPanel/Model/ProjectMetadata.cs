//using System;
//using System.IO;
//using System.Text.Json.Serialization;
//using System.Threading.Tasks;
//using VirtualPaper.Common;
//using VirtualPaper.Common.Utils.Storage;

//namespace VirtualPaper.DraftPanel.Model {
//    [JsonSerializable(typeof(ProjectMetadata))]
//    internal partial class ProjectMetadataContext : JsonSerializerContext { }

//    internal class ProjectMetadata {
//        public string Name { get; init; }
//        public ProjectType Type { get; init; }
//        public string EntryRelativePath { get; init; }

//        [JsonConstructor]
//        [Obsolete("This constructor is intended for JSON deserialization only. Use the another method instead.")]
//        public ProjectMetadata(string name, ProjectType type, string entryRelativePath) {
//            Name = name;
//            Type = type;
//            EntryRelativePath = entryRelativePath;
//        }

//        public ProjectMetadata(string projectName, ProjectType projectType) {
//            string entryFileExtension = projectType switch { 
//                ProjectType.PImage => FileExtension.FE_STATIC_IMG_PROJ, 
//                _ => FileExtension.FE_STATIC_IMG_PROJ 
//            };

//            Name = projectName;
//            Type = projectType;
//            EntryRelativePath = $"{projectName}{entryFileExtension}";
//        }

//        internal async Task SaveAsync(string storageFolderPath) {
//            await JsonSaver.SaveAsync(
//                Path.Combine(storageFolderPath, Name + FileExtension.FE_Project),
//                this,
//                ProjectMetadataContext.Default);
//        }

//        internal static async Task<ProjectMetadata> LoadAsync(string filePath) {
//            return await JsonSaver.LoadAsync<ProjectMetadata>(filePath, ProjectMetadataContext.Default);
//        }
//    }
//}
