using System;

namespace VirtualPaper.IntelligentPanel.Models {
    public record StyleTransferData : IIntelliData {
        public Guid Id { get; }
        public string SourceFilePath { get; }
        public string SourceResolution => $"{Width} * {Height}";
        public string SourceFileSize { get; }
        public string SourceFileExt { get; }

        public string StyleFilePath { get; }
        public string? StyleName { get; }
        public string StyleFileSize { get; }
        public string StyleFileExt { get; }

        public string? ResultFilePath { get; private set; }

        public uint Width { get; }
        public uint Height { get; }

        public string EstimatedTime { get; }

        public StyleTransferData(
            string sourcePath, string sourceFileSize, string sourceFileExt, uint width, uint height,
            string styleImagePath, string? styleName, string styleFileSize, string styleFileExt,
            string estimatedTime) {
            Id = Guid.NewGuid();
            SourceFilePath = sourcePath;
            Width = width;
            Height = height;
            SourceFileSize = sourceFileSize;
            SourceFileExt = sourceFileExt;
            StyleFilePath = styleImagePath;
            StyleName = styleName;
            StyleFileSize = styleFileSize;
            StyleFileExt = styleFileExt;
            EstimatedTime = estimatedTime;
        }
    }
}
