namespace VirtualPaper.IntelligentPanel.Models {
    public record StyleTransferInput : IIntelliData {
        public string SourceFilePath { get; set; }
        public string StyleFilePath { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }

        public StyleTransferInput(string sourceFilePath, string styleFilePath, uint width, uint height) {
            SourceFilePath = sourceFilePath;
            StyleFilePath = styleFilePath;
            Width = width;
            Height = height;
        }
    }
}
