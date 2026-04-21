using System.Text.Json.Serialization;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Models.RepoPanel.Interfaces;

namespace VirtualPaper.Models.RepoPanel {
    [JsonSerializable(typeof(DpBasicData))]
    [JsonSerializable(typeof(IDpBasicData))]
    public partial class DpBasicDataContext : JsonSerializerContext { }

    public class DpBasicData : ObservableObject, IDpBasicData {
        public string EntryFile { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public float DefaultScale { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Dictionary<string, string> Actions { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DeskPetEngineType Type { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Uid { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ApplicationInfo AppInfo { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Title { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Desc { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Authors { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string PublishDate { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double Rating { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Partition { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Tags { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string FolderName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string FolderPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string FilePath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ThumbnailPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string FileSize { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string FileExtension { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime CreatedTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsSubscribed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IDpBasicData Clone() {
            throw new NotImplementedException();
        }

        public bool Equals(IDpBasicData? other) {
            throw new NotImplementedException();
        }

        public bool IsAvailable() {
            throw new NotImplementedException();
        }

        public void Merge(IDpBasicData oldData) {
            throw new NotImplementedException();
        }

        public Task MoveToAsync(string targetFolderPath) {
            throw new NotImplementedException();
        }

        public void Read(string filePath) {
            throw new NotImplementedException();
        }

        public void Save() {
            throw new NotImplementedException();
        }

        public Task SaveAsync() {
            throw new NotImplementedException();
        }
    }
}
