using System.Threading.Tasks;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.EditPanel.Services {
    public interface IWorkspaceSaveCoordinator {
        Task<bool> CanCloseAsync(IRuntime runtime, bool isSaved, bool canCancel);
    }
}
