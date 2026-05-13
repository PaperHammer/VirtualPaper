namespace VirtualPaper.ML.SuperResolution.Interfaces {
    public interface ISuperResolution : IDisposable {
        string ModelPath { get; }
        
        void LoadModel(string? path = null);
        string RunAndSave(
            string inputImagePath,
            string outputFilePath,
            uint targetWidth,
            uint targetHeight);
    }
}
