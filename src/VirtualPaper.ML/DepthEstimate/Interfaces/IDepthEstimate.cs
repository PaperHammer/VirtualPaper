using VirtualPaper.ML.DepthEstimate.Models;

namespace VirtualPaper.ML.DepthEstimate.Interfaces {
    public interface IDepthEstimate : IDisposable {
        void LoadModel(string? path = null);
        DepthEstimateModelOutput Run(string imagePath);
        string SaveDepthMap(DepthEstimateModelOutput modelOutput, string outputFolder);
        string ModelPath { get; }
    }
}
