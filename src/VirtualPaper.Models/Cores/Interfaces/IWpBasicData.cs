using VirtualPaper.Common;

namespace VirtualPaper.Models.Cores.Interfaces {
    /// <summary>
    /// 壁纸核心数据接口
    /// </summary>
    public interface IWpBasicData : IBasicAssetData<IWpBasicData> {
        string WallpaperUid { get; set; }
        string Resolution { get; set; }
        string AspectRatio { get; set; }

        /// <summary>
        /// 壁纸文件类型
        /// </summary>
        WpFileType FType { get; set; }

        /// <summary>
        /// 标识是否在生成时是否存在多种形式
        /// </summary>
        bool IsSingleRType { get; set; }
    }
}
