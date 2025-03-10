using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    public class WpPlayerData : IWpPlayerData {
        public string WallpaperUid { get; set; } = string.Empty;
        public RuntimeType RType { get; set; } = RuntimeType.RUnknown;
        public string FilePath { get; set; } = string.Empty;
        public string DepthFilePath { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public string WpEffectFilePathTemplate { get; set; } = string.Empty;
        public string WpEffectFilePathTemporary { get; set; } = string.Empty;
        public string WpEffectFilePathUsing { get; set; } = string.Empty;

        public IWpMetadata GetMetadata(string monitorContent) {
            WpMetadata metadata = new() {
                BasicData = JsonStorage.Load<WpBasicData>(Path.Combine(FolderPath, Constants.Field.WpBasicDataFileName), WpBasicDataContext.Default),
                RuntimeData = JsonStorage.Load<WpRuntimeData>(Path.Combine(FolderPath, monitorContent, RType.ToString(), Constants.Field.WpRuntimeDataFileName), WpRuntimeDataContext.Default),
            };

            return metadata;
        }
    }
}
