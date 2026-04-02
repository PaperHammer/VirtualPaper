using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.UndoRedo.Events;

namespace Workloads.Utils.DraftUtils.Interfaces {
    public interface IRuntime {
        event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;
        Type ExportOverlayPageType { get; }
        Task<bool> SaveAsync();
        Task UndoAsync();
        Task RedoAsync();
        //Task ExportAsync(IExportData data);
        IAsyncEnumerable<string> ExportAsync(IExportData data, CancellationToken token = default);
    }
}
