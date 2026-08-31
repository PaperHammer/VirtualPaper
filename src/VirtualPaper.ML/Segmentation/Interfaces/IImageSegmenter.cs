using VirtualPaper.ML.Segmentation.Models;

namespace VirtualPaper.ML.Segmentation.Interfaces {
    public interface IImageSegmenter : IDisposable {
        string EncoderModelPath { get; }
        string DecoderModelPath { get; }

        void LoadModels(string? encoderPath = null, string? decoderPath = null);

        SegmentationModelOutput Run(
            string imagePath,
            IReadOnlyList<SegmentationBox> boxes,
            MobileSamOptions? options = null,
            CancellationToken ct = default);
    }
}
