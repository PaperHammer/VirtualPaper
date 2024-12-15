using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.Models.Cores {
    [Serializable]
    // WinRT 接口?
    public partial class WpMetadata : DynamicPropertyChanged, IWpMetadata {
        public IWpBasicData BasicData {
            get => Get(() => BasicData);
            set => Set(() => BasicData, value);
        }

        public IWpRuntimeData RuntimeData { get; set; }

        public WpMetadata() {
            BasicData = new WpBasicData();
            RuntimeData = new WpRuntimeData();
        }

        public void Read(string folderPath) {
            BasicData.Read(Path.Combine(folderPath, Constants.Field.WpBasicDataFileName));
            RuntimeData.Read(Path.Combine(folderPath, Constants.Field.WpRuntimeDataFileName));
        }

        public void MoveTo(string targetFolderPath) {
            BasicData.MoveTo(targetFolderPath);
            RuntimeData.MoveTo(targetFolderPath);
        }

        public void Save() {
            BasicData.Save();
            RuntimeData.Save();
        }

        public IWpPlayerData GetPlayerData() {
            IWpPlayerData playerData = new WpPlayerData() {
                WallpaperUid = BasicData.WallpaperUid,
                RType = RuntimeData.RType,
                FilePath = BasicData.FilePath,
                FolderPath = BasicData.FolderPath,
                WpEffectFilePathUsing = RuntimeData.WpEffectFilePathUsing,
                WpEffectFilePathTemplate = RuntimeData.WpEffectFilePathTemplate,
                WpEffectFilePathTemporary = RuntimeData.WpEffectFilePathTemporary,
                ThumbnailPath = BasicData.ThumbnailPath,
                DepthFilePath = RuntimeData.DepthFilePath,
            };

            return playerData;
        }

        public bool IsAvailable() {
            return BasicData.IsAvailable() && RuntimeData.IsAvailable();
        }
    }
}
