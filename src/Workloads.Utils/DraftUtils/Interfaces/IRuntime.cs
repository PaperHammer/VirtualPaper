using System;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using Workloads.Utils.DraftUtils.Models;

namespace Workloads.Utils.DraftUtils.Interfaces {
    public interface IRuntime {
        event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;
        string FileName { get; }
        string FileNameWithoutEx { get; }
        string Id { get; }
        bool IsSavedFromInit { get; }

        /// <summary>
        /// 当前运行时对应的项目类型（FDesign=静态图项目，FWebDesign=Web 项目），
        /// 用于按项目类型调整共享菜单（如导出菜单）的可用项。
        /// </summary>
        FileType RuntimeFileType { get; }

        /// <summary>
        /// 由 RuntimeFactory 在构造后调用，完成文件相关的初始化
        /// </summary>
        void Initialize(string filePath, FileType fileType);
        Task<bool> SaveAsync();
        Task<string?> SaveAsAsync();
        Task UndoAsync();
        Task RedoAsync();
        Task<string?> ExportAsync(ExportImageFormat format);

        /// <summary>
        /// 一键入库：将当前作品打包并导入壁纸库（与导出交互一致，由共享的“文件 → 入库”菜单触发）。
        /// </summary>
        Task<bool> AddToLibraryAsync();
    }
}
