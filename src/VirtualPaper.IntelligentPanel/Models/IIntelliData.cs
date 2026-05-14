using System;

namespace VirtualPaper.IntelligentPanel.Models {
    public interface IIntelliData {
        public Guid Id { get; }
        string SourceFilePath { get; }
    }
}
