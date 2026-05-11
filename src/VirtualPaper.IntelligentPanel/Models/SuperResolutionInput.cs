namespace VirtualPaper.IntelligentPanel.Models {
    public record SuperResolutionInput : IIntelliData {
        public string SourceFilePath { get; set; }
        public uint TargetWidth { get; set; }
        public uint TargetHeight { get; set; }

        public SuperResolutionInput(string sourceFilePath, uint targetWidth, uint targetHeight) {
            SourceFilePath = sourceFilePath;
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
        }
    }
}
