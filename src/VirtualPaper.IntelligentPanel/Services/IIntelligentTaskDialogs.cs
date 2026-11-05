using System.Threading.Tasks;

namespace VirtualPaper.IntelligentPanel.Services {
    public readonly record struct TaskCloseDecision(bool Close, bool DontRemind);

    public interface IIntelligentTaskDialogs {
        Task<TaskCloseDecision> ConfirmCloseAsync(int activeCount, int temporaryCount, bool allowDontRemind);
        Task<bool> ConfirmRemoveAsync();
    }
}
