using System.Threading.Tasks;

namespace VirtualPaper.PlayerWeb.Core.Interfaces {
    public interface IApplyService {
        ValueTask ApplyAsync(object? context = null);
    }
}
