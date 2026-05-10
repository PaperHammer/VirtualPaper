using System;

namespace VirtualPaper.IntelligentPanel.Models {
    public record StyleTransferOutput {
        public Guid Id { get; }
        public string SourceFilePath { get; }
        public string StyleFilePath { get; }
        public string? ResultFilePath { get; private set; }
        public string? Resolution { get; private set; }

        public StyleTransferOutput(string sourcePath, string stylePath) {
            Id = Guid.NewGuid();
            SourceFilePath = sourcePath;
            StyleFilePath = stylePath;
        }
    }
}
