using System.IO;
using System.Text.Json.Serialization;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.Models.Cores {
    [JsonSerializable(typeof(WpBasicData))]
    [JsonSerializable(typeof(IWpBasicData))]
    public partial class WpBasicDataContext : JsonSerializerContext { }

    public class WpBasicData : ObservableObject, IWpBasicData {
        public string WallpaperUid { get; set; } = string.Empty;
        public ApplicationInfo AppInfo { get; set; } = new();

        private string _title = string.Empty;
        public string Title {
            get => _title;
            set { if (_title == value) return; _title = value; OnPropertyChanged(); }
        }

        private string _desc = string.Empty;
        public string Desc {
            get => _desc;
            set { if (_desc == value) return; _desc = value; OnPropertyChanged(); }
        }
        public string Authors { get; set; } = string.Empty;
        public string PublishDate { get; set; } = string.Empty;
        public double Rating { get; set; } = -1;

        private FileType _fType;
        public FileType FType {
            get => _fType;
            set {
                _fType = Enum.IsDefined(value) ? value : FileType.FUnknown;
                this.IsSingleRType =
                     _fType switch {
                         FileType.FImage or FileType.FGif => false,
                         FileType.FVideo => true,
                         FileType.FWebZip => true,
                         _ => false,
                     };
            }
        }
        public bool IsSingleRType { get; set; } = false;
        public string Partition { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty; // splite with ';'

        public string FolderName { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;

        public string Resolution { get; set; } = string.Empty;
        public string AspectRatio { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }

        public bool IsSubscribed { get; set; } = false;

        public IWpBasicData Clone() {
            IWpBasicData data = new WpBasicData() {
                WallpaperUid = this.WallpaperUid,
                AppInfo = this.AppInfo,
                Title = this.Title,
                Desc = this.Desc,
                Authors = this.Authors,
                PublishDate = this.PublishDate,
                Rating = this.Rating,
                FType = this.FType,
                IsSingleRType = this.IsSingleRType,
                Partition = this.Partition,
                Tags = this.Tags,

                FolderName = this.FolderName,
                FolderPath = this.FolderPath,
                FilePath = this.FilePath,
                ThumbnailPath = this.ThumbnailPath,

                Resolution = this.Resolution,
                AspectRatio = this.AspectRatio,
                FileSize = this.FileSize,
                FileExtension = this.FileExtension,
                CreatedTime = this.CreatedTime,

                IsSubscribed = this.IsSubscribed,
            };

            return data;
        }

        public void InitData(WpBasicData source) {
            if (source == null) return;

            this.WallpaperUid = source.WallpaperUid;
            this.AppInfo = source.AppInfo;
            this.Title = source.Title;
            this.Desc = source.Desc;
            this.Authors = source.Authors;
            this.PublishDate = source.PublishDate;
            this.Rating = source.Rating;
            this.FType = source.FType;
            this.IsSingleRType = source.IsSingleRType;
            this.Partition = source.Partition;
            this.Tags = source.Tags;

            this.FolderName = source.FolderName;
            this.FolderPath = source.FolderPath;
            this.FilePath = source.FilePath;
            this.ThumbnailPath = source.ThumbnailPath;

            this.Resolution = source.Resolution;
            this.AspectRatio = source.AspectRatio;
            this.FileSize = source.FileSize;
            this.FileExtension = source.FileExtension;
            this.CreatedTime = source.CreatedTime;

            this.IsSubscribed = source.IsSubscribed;
        }

        public void Read(string filePath) {
            InitData(Load(filePath));
        }

        public static WpBasicData Load(string filePath) {
            var data = JsonSaver.Load<WpBasicData>(filePath, WpBasicDataContext.Default)
                ?? throw new InvalidDataException($"Wallpaper metadata is empty: {filePath}");
            data.ResolvePaths(filePath);
            return data;
        }

        public static async Task<WpBasicData?> LoadAsync(string filePath) {
            var data = await JsonSaver.LoadAsync<WpBasicData>(filePath, WpBasicDataContext.Default);
            data?.ResolvePaths(filePath);
            return data;
        }

        public void ResolvePaths(string metadataPath) {
            string directory = WallpaperMetadataPaths.DirectoryOf(metadataPath);
            FolderPath = WallpaperMetadataPaths.Resolve(directory, FolderPath);
            FilePath = WallpaperMetadataPaths.Resolve(directory, FilePath);
            ThumbnailPath = WallpaperMetadataPaths.Resolve(directory, ThumbnailPath);
        }

        public void Save() {
            SaveAsync().GetAwaiter().GetResult();
        }

        public async Task SaveAsync() {
            await SaveMetadataAsync();
            await SaveProjectJsonAsync();
        }

        /// <summary>Save library metadata without changing the packaged Web project's manifest.</summary>
        public Task SaveMetadataAsync() => SaveMetadataAsync(Constants.CoreField.FileVersion);

        /// <summary>Save a migration's target version, rather than prematurely recording the latest version.</summary>
        public async Task SaveMetadataAsync(string fileVersion) {
            string metadataPath = Path.Combine(FolderPath, Constants.Field.WpBasicDataFileName);
            string directory = WallpaperMetadataPaths.DirectoryOf(metadataPath);
            var stored = (WpBasicData)Clone();
            stored.AppInfo = new() { AppName = AppInfo.AppName, AppVersion = AppInfo.AppVersion, FileVersion = fileVersion };
            stored.FolderPath = WallpaperMetadataPaths.Relative(directory, FolderPath);
            stored.FilePath = WallpaperMetadataPaths.Relative(directory, FilePath);
            stored.ThumbnailPath = WallpaperMetadataPaths.Relative(directory, ThumbnailPath);
            await JsonSaver.SaveAsync<IWpBasicData>(
                metadataPath,
                stored,
                WpBasicDataContext.Default);
            AppInfo.FileVersion = fileVersion;
        }

        private async Task SaveProjectJsonAsync() {
            if (this.FType != FileType.FWebZip) return;

            string projectJsonPath = Path.Combine(this.FolderPath, "project.json");
            if (!File.Exists(projectJsonPath)) return;

            var project = await JsonSaver.LoadAsync<WpWebProjectData>(projectJsonPath, WpWebProjectDataContext.Default);
            project.Title = this.Title;
            project.Desc = this.Desc;
            project.Tags = this.Tags;
            await JsonSaver.SaveAsync(projectJsonPath, project, WpWebProjectDataContext.Default);
        }

        public async Task MoveToAsync(string targetFolderPath) {
            if (!Directory.Exists(targetFolderPath)) {
                FileUtil.CopyDirectory(
                    this.FolderPath,
                    targetFolderPath,
                    true);
            }
            string oldFolderPath = this.FolderPath;

            if (oldFolderPath != targetFolderPath) {
                this.FolderPath = await FileUtil.UpdateFileFolderPathAsync(this.FolderPath, oldFolderPath, targetFolderPath);
                this.FilePath = await FileUtil.UpdateFileFolderPathAsync(this.FilePath, oldFolderPath, targetFolderPath);
                this.ThumbnailPath = await FileUtil.UpdateFileFolderPathAsync(this.ThumbnailPath, oldFolderPath, targetFolderPath);
            }

            Save();
        }

        public void Merge(IWpBasicData oldData) {
            if (oldData == null) return;

            this.Title = oldData.Title;
            this.Desc = oldData.Desc;
            this.Authors = oldData.Authors;
            this.PublishDate = oldData.PublishDate;
            this.Rating = oldData.Rating;
            this.IsSingleRType = oldData.IsSingleRType;
            this.Partition = oldData.Partition;
            this.Tags = oldData.Tags;
            this.IsSubscribed = oldData.IsSubscribed;
        }

        public bool IsAvailable() {
            // 缩略图允许为空（如 Web 壁纸包缺少 preview 时），UI 层用原生占位图兜底显示
            return this.FType != FileType.FUnknown
                && this.WallpaperUid != string.Empty
                && this.AppInfo.AppVersion != string.Empty;
        }

        public bool Equals(IWpBasicData? other) {
            return other != null && other.WallpaperUid == this.WallpaperUid && other.FilePath == this.FilePath;
        }
    }
}
