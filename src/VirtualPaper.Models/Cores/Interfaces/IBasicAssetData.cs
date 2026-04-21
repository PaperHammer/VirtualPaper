using System.ComponentModel;

namespace VirtualPaper.Models.Cores.Interfaces {
    /// <summary>
    /// 资源（壁纸/桌宠等）的基础公共数据接口
    /// 使用泛型 T 来约束 IEquatable、Clone 和 Merge 的强类型
    /// </summary>
    public interface IBasicAssetData<T> : IEquatable<T>, INotifyPropertyChanged where T : IBasicAssetData<T> {
        /// <summary>
        /// 资源唯一标识符
        /// </summary>
        string Uid { get; set; }

        /// <summary>
        /// 生成或发布时所使用的 app 信息
        /// </summary>
        ApplicationInfo AppInfo { get; set; }

        string Title { get; set; }
        string Desc { get; set; }
        string Authors { get; set; }
        string PublishDate { get; set; }
        double Rating { get; set; }
        string Partition { get; set; }
        string Tags { get; set; }
        string FolderName { get; set; }
        string FolderPath { get; set; }
        string FilePath { get; set; }
        string ThumbnailPath { get; set; }
        string FileSize { get; set; }
        string FileExtension { get; set; }
        DateTime CreatedTime { get; set; }

        /// <summary>
        /// 是否被订阅（已下载/已入库）
        /// </summary>
        bool IsSubscribed { get; set; }

        // --- 公共行为 ---
        void Read(string filePath);
        Task MoveToAsync(string targetFolderPath);
        void Save();
        Task SaveAsync();
        bool IsAvailable();

        // --- 泛型强类型行为 ---
        T Clone();
        void Merge(T oldData);
    }
}
