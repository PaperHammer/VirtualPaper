using VirtualPaper.Models.Cores;

namespace VirtualPaper.Utils.BasicDataBuilders {
    /// <summary>
    /// 针对不同 FileType，填充 WpBasicData 中类型特定的字段。
    /// 通用字段（WallpaperUid / AppInfo / FolderPath 等）由调用方在调用前统一设置。
    /// </summary>
    internal interface IWpBasicDataBuilder {
        /// <summary>
        /// 根据源文件构建类型特定的元数据，写入 <paramref name="data"/>。
        /// </summary>
        /// <param name="srcPath">源文件路径（图片/视频/zip）</param>
        /// <param name="folderPath">已创建的库目标文件夹路径</param>
        /// <param name="folderName">文件夹名（用于统一命名缩略图等产物）</param>
        /// <param name="data">待填充的数据对象</param>
        /// <param name="token">取消令牌</param>
        void Build(string srcPath, string folderPath, string folderName, WpBasicData data, CancellationToken token);
    }
}
