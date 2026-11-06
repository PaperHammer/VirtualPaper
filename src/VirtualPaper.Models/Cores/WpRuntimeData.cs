using System.IO;
using System.Text.Json.Serialization;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    [JsonSerializable(typeof(WpRuntimeData))]
    [JsonSerializable(typeof(IWpRuntimeData))]
    public partial class WpRuntimeDataContext : JsonSerializerContext { }

    public class WpRuntimeData : IWpRuntimeData {
        public ApplicationInfo AppInfo { get; set; } = new();
        public string MonitorContent { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string DepthFilePath { get; set; } = string.Empty;
        public string WpEffectFilePathTemplate { get; set; } = string.Empty;
        public string WpEffectFilePathUsing { get; set; } = string.Empty;
        public string WpEffectFilePathTemporary { get; set; } = string.Empty;
        public RuntimeType RType { get; set; } = RuntimeType.RUnknown;

        public void Read(string filePath) {
            InitData(Load(filePath));
        }

        public static WpRuntimeData Load(string filePath) {
            var data = JsonSaver.Load<WpRuntimeData>(filePath, WpRuntimeDataContext.Default)
                ?? throw new InvalidDataException($"Wallpaper runtime metadata is empty: {filePath}");
            data.ResolvePaths(filePath);
            return data;
        }

        public static async Task<WpRuntimeData> LoadAsync(string filePath) {
            var data = await JsonSaver.LoadAsync<WpRuntimeData>(filePath, WpRuntimeDataContext.Default)
                ?? throw new InvalidDataException($"Wallpaper runtime metadata is empty: {filePath}");
            data.ResolvePaths(filePath);
            return data;
        }

        public void ResolvePaths(string metadataPath) {
            string directory = WallpaperMetadataPaths.DirectoryOf(metadataPath);
            FolderPath = WallpaperMetadataPaths.Resolve(directory, FolderPath);
            DepthFilePath = WallpaperMetadataPaths.Resolve(directory, DepthFilePath);
            WpEffectFilePathTemplate = WallpaperMetadataPaths.Resolve(directory, WpEffectFilePathTemplate);
            WpEffectFilePathUsing = WallpaperMetadataPaths.Resolve(directory, WpEffectFilePathUsing);
            WpEffectFilePathTemporary = WallpaperMetadataPaths.Resolve(directory, WpEffectFilePathTemporary);
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
                this.FolderPath = this.FolderPath.Replace(oldFolderPath, targetFolderPath);
                this.DepthFilePath = await FileUtil.UpdateFileFolderPathAsync(this.DepthFilePath, oldFolderPath, targetFolderPath);
                this.WpEffectFilePathTemplate = await FileUtil.UpdateFileFolderPathAsync(this.WpEffectFilePathTemplate, oldFolderPath, targetFolderPath);
                this.WpEffectFilePathUsing = await FileUtil.UpdateFileFolderPathAsync(this.WpEffectFilePathUsing, oldFolderPath, targetFolderPath);
                this.WpEffectFilePathTemporary = await FileUtil.UpdateFileFolderPathAsync(this.WpEffectFilePathTemporary, oldFolderPath, targetFolderPath);
            }

            Save();
        }

        public async Task FromTempMoveToInstallPathAsync(string targetFolderPath) {
            string oldFolderPath = Constants.CommonPaths.TempDir;
            this.FolderPath = this.FolderPath.Replace(oldFolderPath, targetFolderPath);
            this.DepthFilePath = await FileUtil.UpdateFileFolderPathAsync(this.DepthFilePath, oldFolderPath, targetFolderPath);
            this.WpEffectFilePathTemplate = await FileUtil.UpdateFileFolderPathAsync(this.WpEffectFilePathTemplate, oldFolderPath, targetFolderPath);
            this.WpEffectFilePathUsing = await FileUtil.UpdateFileFolderPathAsync(this.WpEffectFilePathUsing, oldFolderPath, targetFolderPath);
            this.WpEffectFilePathTemporary = await FileUtil.UpdateFileFolderPathAsync(this.WpEffectFilePathTemporary, oldFolderPath, targetFolderPath);

            Save();
        }

        public void Save() => Save(Constants.CoreField.FileVersion);

        /// <summary>Save the target version of a single migration step.</summary>
        public void Save(string fileVersion) {
            if (MonitorContent == string.Empty) {
                throw new Exception("Save failed");
            }
            string metadataPath = Path.Combine(FolderPath, MonitorContent, RType.ToString(), Constants.Field.WpRuntimeDataFileName);
            string directory = WallpaperMetadataPaths.DirectoryOf(metadataPath);
            var stored = (WpRuntimeData)Clone();
            stored.AppInfo = new() { AppName = AppInfo.AppName, AppVersion = AppInfo.AppVersion, FileVersion = fileVersion };
            stored.FolderPath = WallpaperMetadataPaths.Relative(directory, FolderPath);
            stored.DepthFilePath = WallpaperMetadataPaths.Relative(directory, DepthFilePath);
            stored.WpEffectFilePathTemplate = WallpaperMetadataPaths.Relative(directory, WpEffectFilePathTemplate);
            stored.WpEffectFilePathUsing = WallpaperMetadataPaths.Relative(directory, WpEffectFilePathUsing);
            stored.WpEffectFilePathTemporary = WallpaperMetadataPaths.Relative(directory, WpEffectFilePathTemporary);
            JsonSaver.Save<IWpRuntimeData>(
                metadataPath,
                stored,
                WpRuntimeDataContext.Default);
            AppInfo.FileVersion = fileVersion;
        }

        public IWpRuntimeData Clone() {
            IWpRuntimeData data = new WpRuntimeData() {
                AppInfo = this.AppInfo,
                MonitorContent = this.MonitorContent,
                FolderPath = this.FolderPath,
                DepthFilePath = this.DepthFilePath,
                WpEffectFilePathTemplate = this.WpEffectFilePathTemplate,
                WpEffectFilePathTemporary = this.WpEffectFilePathTemporary,
                WpEffectFilePathUsing = this.WpEffectFilePathUsing,
                RType = this.RType,
            };

            return data;
        }

        public void InitData(WpRuntimeData source) {
            if (source == null) return;

            this.AppInfo = source.AppInfo;
            this.MonitorContent = source.MonitorContent;
            this.FolderPath = source.FolderPath;
            this.DepthFilePath = source.DepthFilePath;
            this.WpEffectFilePathTemplate = source.WpEffectFilePathTemplate;
            this.WpEffectFilePathTemporary = source.WpEffectFilePathTemporary;
            this.WpEffectFilePathUsing = source.WpEffectFilePathUsing;
            this.RType = source.RType;
        }

        public bool IsAvailable() {
            return this.FolderPath != string.Empty && this.AppInfo.AppVersion != string.Empty;
        }
    }
}
