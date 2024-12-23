using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    [Serializable]
    public class WpRuntimeData : IWpRuntimeData {
        public ApplicationInfo AppInfo { get; set; } = new();
        public string MonitorContent { get; set; } = "-1";
        public string FolderPath { get; set; } = string.Empty;
        public string DepthFilePath { get; set; } = string.Empty;
        public string WpEffectFilePathTemplate { get; set; } = string.Empty;
        public string WpEffectFilePathUsing { get; set; } = string.Empty;
        public string WpEffectFilePathTemporary { get; set; } = string.Empty;
        public RuntimeType RType { get; set; } = RuntimeType.RUnknown;

        public void Read(string filePath) {
            var data = JsonStorage<WpRuntimeData>.LoadData(filePath);
            InitData(data);
        }

        public void MoveTo(string targetFolderPath) {
            if (!Directory.Exists(targetFolderPath)) {
                FileUtil.CopyDirectory(
                    this.FolderPath,
                    targetFolderPath,
                    true);
            }
            string oldFolderPath = this.FolderPath;

            if (oldFolderPath != targetFolderPath) {
                this.FolderPath = this.FolderPath.Replace(oldFolderPath, targetFolderPath);
                this.DepthFilePath = this.DepthFilePath.Replace(oldFolderPath, targetFolderPath);
                this.WpEffectFilePathTemplate = this.WpEffectFilePathTemplate.Replace(oldFolderPath, targetFolderPath);
                this.WpEffectFilePathUsing = this.WpEffectFilePathUsing.Replace(oldFolderPath, targetFolderPath);
                this.WpEffectFilePathTemporary = this.WpEffectFilePathTemporary.Replace(oldFolderPath, targetFolderPath);
            }

            Save();
        }

        public void FromTempToInstallPath(string targetFolderPath) {
            string oldFolderPath = Constants.CommonPaths.TempDir;
            this.FolderPath = this.FolderPath.Replace(oldFolderPath, targetFolderPath);
            this.DepthFilePath = this.DepthFilePath.Replace(oldFolderPath, targetFolderPath);
            this.WpEffectFilePathTemplate = this.WpEffectFilePathTemplate.Replace(oldFolderPath, targetFolderPath);
            this.WpEffectFilePathUsing = this.WpEffectFilePathUsing.Replace(oldFolderPath, targetFolderPath);
            this.WpEffectFilePathTemporary = this.WpEffectFilePathTemporary.Replace(oldFolderPath, targetFolderPath);

            Save();
        }

        public void Save() {
            JsonStorage<IWpRuntimeData>.StoreData(Path.Combine(this.FolderPath, MonitorContent, RType.ToString(), Constants.Field.WpRuntimeDataFileName), this);
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

        public void RevertToApplied() {
            File.Copy(this.WpEffectFilePathUsing, this.WpEffectFilePathTemporary, true);
        }

        public void RevertToDefault() {
            File.Copy(this.WpEffectFilePathTemplate, this.WpEffectFilePathTemporary, true);
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
