using System;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using Workloads.Utils.DraftUtils.Models;

namespace Workloads.Utils.DraftUtils.Interfaces {
    public interface IRuntime {
        event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;
        string FileName { get; }
        string FileNameWithoutEx { get; }
        string Id { get; }
        Task<bool> SaveAsync();
        Task UndoAsync();
        Task RedoAsync();
        Task ExportAsync(ExportImageFormat format);
    }
}
