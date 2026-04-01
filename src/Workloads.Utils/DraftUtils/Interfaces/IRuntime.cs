using System;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using Workloads.Utils.DraftUtils.Models;

namespace Workloads.Utils.DraftUtils.Interfaces {
    public interface IRuntime {
        event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;
        Task<bool> SaveAsync();
        Task UndoAsync();
        Task RedoAsync();
        Task ExportAsync(ExportDataStaticImg data);
    }
}
