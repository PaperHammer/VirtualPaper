using VirtualPaper.Common;

namespace VirtualPaper.Models.Cores.Interfaces {
    public interface IWpBasicData {
        /// <summary>
        /// 资源唯一标识符
        /// </summary>
        string WallpaperUid { get; set; }

        /// <summary>
        /// 生成或发布时所使用的 app 信息
        /// </summary>
        ApplicationInfo AppInfo { get; set; }

        /// <summary>
        /// 标题
        /// </summary>
        string Title { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        string Desc { get; set; }

        /// <summary>
        /// 作者
        /// </summary>
        string Authors { get; set; }

        /// <summary>
        /// 发布日期
        /// </summary>
        string PublishDate { get; set; }

        /// <summary>
        /// 评分
        /// </summary>
        double Rating { get; set; }

        /// <summary>
        /// 分区
        /// </summary>
        string Partition { get; set; }

        /// <summary>
        /// 标签
        /// </summary>
        string Tags { get; set; }

        string FolderName { get; set; }

        /// <summary>
        /// 存储文件夹目录
        /// </summary>
        string FolderPath { get; set; }

        /// <summary>
        /// 壁纸存储位置
        /// </summary>
        string FilePath { get; set; }

        /// <summary>
        /// 缩略图存储位置
        /// </summary>
        string ThumbnailPath { get; set; }

        string Resolution { get; set; }
        string AspectRatio { get; set; }
        string FileSize { get; set; }
        string FileExtension { get; set; }

        /// <summary>
        /// 壁纸文件类型
        /// </summary>
        FileType FType { get; set; }
        /// <summary>
        /// 标识是否在生成是是否存在多种形式
        /// </summary>
        bool IsSingleRType { get; set; }

        /// <summary>
        /// 是否被订阅（已下载/已入库）
        /// </summary>
        bool IsSubscribed { get; set; }

        void Read(string filePath);
        void MoveTo(string targetFolderPath);
        void Save();
        IWpBasicData Clone();
        bool IsAvailable();
    }
}
