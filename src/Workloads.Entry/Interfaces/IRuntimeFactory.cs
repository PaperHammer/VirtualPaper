using VirtualPaper.Common;
using Workloads.Utils.DraftUtils.Interfaces;

namespace Workloads.Entry.Interfaces {
    public interface IRuntimeFactory {
        IRuntime Create(string file, FileType type);
    }
}
