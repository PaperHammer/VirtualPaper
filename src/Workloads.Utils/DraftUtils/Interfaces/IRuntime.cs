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
        /// 由 RuntimeFactory 在构造后调用，完成文件相关的初始化
        /// </summary>
        void Initialize(string filePath, FileType fileType);
        Task<bool> SaveAsync();
        Task<string?> SaveAsAsync();
        Task UndoAsync();
        Task RedoAsync();
        Task<string?> ExportAsync(ExportImageFormat format);
    }
}
