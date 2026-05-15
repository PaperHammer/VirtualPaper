namespace VirtualPaper.ML.StyleTransfer.Interfaces {
    public interface IStyleTransfer : IDisposable {
        string ModelPath { get; }
        
        void LoadModel(string? path = null);
        string RunAndSave(
            string contentImagePath,
            string styleImagePath,
            string outputFilePath,
            float alpha = 1.0f,
            int contentSize = 512,
            int styleSize = 512,
            CancellationToken ct = default);
    }
}
