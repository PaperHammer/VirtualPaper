using VirtualPaper.ML.DepthEstimate.Models;

namespace VirtualPaper.ML.DepthEstimate.Interfaces {
    public interface IDepthEstimate : IDisposable {
        void LoadModel(string? path = null);
        DepthEstimateModelOutput Run(string imagePath);
        DepthEstimateModelOutput Run(string imagePath, DepthAnythingOptions? options, CancellationToken ct = default);
        string SaveDepthMap(DepthEstimateModelOutput modelOutput, string outputFolder);
        string ModelPath { get; }
    }
}
