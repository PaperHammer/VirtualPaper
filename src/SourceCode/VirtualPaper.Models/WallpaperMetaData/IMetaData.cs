using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using static VirtualPaper.Models.WallpaperMetaData.MetaData;

namespace VirtualPaper.Models.WallpaperMetaData
{
    public interface IMetaData
    {
        /// <summary>
        /// 资源唯一标识符
        /// </summary>
        string VirtualPaperUid { get; set; }

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
        /// 壁纸类型
        /// </summary>
        WallpaperType Type { get; set; }

        /// <summary>
        /// 装载/运行时状态
        /// </summary>
        RunningState State { get; set; }

        /// <summary>
        /// 分区
        /// </summary>
        string Partition { get; set; }

        /// <summary>
        /// 标签
        /// </summary>
        string Tags { get; set; }

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

        /// <summary>
        /// 壁纸运行时自定义配置文件存储位置
        /// </summary>
        string WpCustomizePath { get; set; } // 还原到最初
        string WpCustomizePathUsing { get; set; } // 还原到当前的应用设置
        string WpCustomizePathTmp { get; set; } // 实时调整与预览

        string Resolution { get; set; }
        string AspectRatio { get; set; }
        string FileSize { get; set; }
        string FileExtension { get; set; }

        /// <summary>
        /// 是否启用为显示器壁纸
        /// </summary>
        bool IsStartup { get; set; }

        /// <summary>
        /// 是否被订阅（已下载/已入库）
        /// </summary>
        bool IsSubscribed { get; set; }

        /// <summary>
        /// 是否正在下载
        /// </summary>
        bool IsDownloading { get; set; }

        /// <summary>
        /// 已下载进度
        /// </summary>
        float DownloadingProgress { get; set; }

        string DownloadingProgressText { get; set; }
    }
}
