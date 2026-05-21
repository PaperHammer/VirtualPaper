using System;

namespace VirtualPaper.PlayerWeb.Core.Interfaces {
    public interface IApplyService {
        void OnApply(ApplyEventArgs args);
    }

    public class ApplyEventArgs : EventArgs {
        public string? WpEffectFilePathUsing { get; internal set; }
        public string? WpEffectFilePathTemplate { get; internal set; }
        public string? WpEffectFilePathTemporary { get; internal set; }
    }
}
