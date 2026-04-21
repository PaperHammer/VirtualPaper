using VirtualPaper.Common;

namespace VirtualPaper.Models.Cores.Interfaces {
    public interface IWpPlayerData {
        string WallpaperUid { get; set; }
        WpRuntimeType RType { get; set; }
        WallpaperArrangement Arrangement { get; set; }
        string FilePath { get; set; }
        string DepthFilePath { get; set; }
        string FolderPath { get; set; }
        string ThumbnailPath { get; set; }
        string WpEffectFilePathTemplate { get; set; }
        string WpEffectFilePathTemporary { get; set; }
        string WpEffectFilePathUsing { get; set; }

        IWpMetadata GetMetadata(string monitorContent);
    }
}
