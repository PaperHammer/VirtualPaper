using System.Threading.Tasks;

namespace VirtualPaper.DraftPanel.Model.Interfaces {
    interface IRuntime {
        Task LoadAsync();
        Task SaveAsync();
    }
}
