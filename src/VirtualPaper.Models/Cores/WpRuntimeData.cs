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
            var data = JsonStorage<WpRuntimeData>.LoadData(filePath, WpRuntimeDataContext.Default);
            InitData(data);
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

        public async void FromTempMoveToInstallPath(string targetFolderPath) {
            string oldFolderPath = Constants.CommonPaths.TempDir;
            this.FolderPath = this.FolderPath.Replace(oldFolderPath, targetFolderPath);
            this.DepthFilePath = await FileUtil.UpdateFileFolderPathAsync(this.DepthFilePath, oldFolderPath, targetFolderPath);
            this.WpEffectFilePathTemplate = await FileUtil.UpdateFileFolderPathAsync(this.WpEffectFilePathTemplate, oldFolderPath, targetFolderPath);
            this.WpEffectFilePathUsing = await FileUtil.UpdateFileFolderPathAsync(this.WpEffectFilePathUsing, oldFolderPath, targetFolderPath);
            this.WpEffectFilePathTemporary = await FileUtil.UpdateFileFolderPathAsync(this.WpEffectFilePathTemporary, oldFolderPath, targetFolderPath);

            Save();
        }

        public void Save() {
            if (MonitorContent == string.Empty) {
                throw new Exception("Save failed");
            }
            JsonStorage<IWpRuntimeData>.StoreData(
                Path.Combine(this.FolderPath, MonitorContent, RType.ToString(), Constants.Field.WpRuntimeDataFileName), 
                this,
                WpRuntimeDataContext.Default);
        }

        public IWpRuntimeData Clone() {
            IWpRuntimeData data = new WpRuntimeData() {
                AppInfo = this.AppInfo,
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
