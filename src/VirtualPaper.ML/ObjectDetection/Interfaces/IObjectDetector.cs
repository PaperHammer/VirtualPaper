using VirtualPaper.ML.ObjectDetection.Models;

namespace VirtualPaper.ML.ObjectDetection.Interfaces {
    public interface IObjectDetector : IDisposable {
        string ModelPath { get; }

        void LoadModel(string? path = null);

        ObjectDetectionModelOutput Run(
            string imagePath,
            ObjectDetectionOptions? options = null,
            CancellationToken ct = default);
    }
}
