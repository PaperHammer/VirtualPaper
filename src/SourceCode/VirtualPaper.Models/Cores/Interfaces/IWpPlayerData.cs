using VirtualPaper.Common;

namespace VirtualPaper.Models.Cores.Interfaces {
    public interface IWpPlayerData {
        string WallpaperUid { get; set; }

        /// <summary>
        /// 壁纸运行时类型
        /// </summary>
        RuntimeType RType { get; set; }
        string FilePath { get; set; }
        string DepthFilePath { get; set; }
        string FolderPath { get; set; }
        string ThumbnailPath { get; set; }
        string WpEffectFilePathTemplate { get; set; }
        string WpEffectFilePathTemporary { get; set; }
        string WpEffectFilePathUsing { get; set; }

        IWpMetadata GetMetadata();
    }
}
