using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using Workloads.Utils.DraftUtils.Models;

namespace Workloads.Utils.DraftUtils.Interfaces {
    public enum RuntimeEditCommand {
        Cut,
        Copy,
        Paste,
        CopyPath,
        CopyRelativePath,
        Rename,
        Delete,
        Find,
        FindInFiles,
        NavigateBack,
        NavigateForward,
        CopyLineUp,
        CopyLineDown,
        MoveLineUp,
        MoveLineDown,
    }

    /// <summary>
    /// 可选的运行时编辑命令提供者，由工作区共享的 Edit 菜单调用。
    /// </summary>
    public interface IRuntimeEditCommandProvider {
        event EventHandler? EditCommandStateChanged;
        bool CanExecuteEditCommand(RuntimeEditCommand command);
        Task ExecuteEditCommandAsync(RuntimeEditCommand command);
    }

    /// <summary>
    /// 可选的工作区顶部栏内容提供者。
    /// </summary>
    public interface IRuntimeTopBarContentProvider {
        FrameworkElement TopBarContent { get; }
        void SetTopBarContentActive(bool isActive);
    }

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
