using System;

namespace VirtualPaper.PlayerWeb.Core.Interfaces {
    public interface IApplyService {
        void OnApply(ApplyEventArgs args);
    }

    public class ApplyEventArgs : EventArgs {
    }
}
