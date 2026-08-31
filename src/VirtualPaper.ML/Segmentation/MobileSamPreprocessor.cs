using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace VirtualPaper.ML.Segmentation {
    internal sealed record MobileSamImageInput(
        DenseTensor<float> Tensor,
        int OriginalWidth,
        int OriginalHeight,
        int ResizedWidth,
        int ResizedHeight,
        float ScaleX,
        float ScaleY);

    internal static class MobileSamPreprocessor {
        internal const int InputSize = 1024;

        // SAM normalization values are in RGB order and operate on 0..255 pixels.
        private static readonly float[] Mean = [123.675f, 116.28f, 103.53f];
        private static readonly float[] Std = [58.395f, 57.12f, 57.375f];

        internal static MobileSamImageInput CreateInput(Mat bgrImage) {
            ArgumentNullException.ThrowIfNull(bgrImage);
            if (bgrImage.Empty() || bgrImage.Type() != MatType.CV_8UC3)
                throw new ArgumentException("MobileSAM input must be a non-empty CV_8UC3 image.", nameof(bgrImage));

            int originalWidth = bgrImage.Width;
            int originalHeight = bgrImage.Height;
            (int resizedWidth, int resizedHeight) = GetPreprocessSize(originalWidth, originalHeight);

            using var resized = new Mat();
            Cv2.Resize(
                bgrImage,
                resized,
                new Size(resizedWidth, resizedHeight),
                0,
                0,
                InterpolationFlags.Linear);

            // DenseTensor starts zero-filled. SAM pads the normalized image on the
            // right and bottom with zeros until it reaches 1024 x 1024.
            var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);
            Span<float> buffer = tensor.Buffer.Span;
            int channelSize = InputSize * InputSize;

            unsafe {
                byte* imagePtr = (byte*)resized.Data;
                int stride = (int)resized.Step();

                for (int y = 0; y < resizedHeight; y++) {
                    byte* row = imagePtr + y * stride;
                    int rowOffset = y * InputSize;

                    for (int x = 0; x < resizedWidth; x++) {
                        int pixelOffset = x * 3;
                        int tensorOffset = rowOffset + x;

                        // OpenCV supplies BGR; MobileSAM expects normalized RGB.
                        buffer[tensorOffset] = (row[pixelOffset + 2] - Mean[0]) / Std[0];
                        buffer[channelSize + tensorOffset] = (row[pixelOffset + 1] - Mean[1]) / Std[1];
                        buffer[2 * channelSize + tensorOffset] = (row[pixelOffset] - Mean[2]) / Std[2];
                    }
                }
            }

            return new MobileSamImageInput(
                tensor,
                originalWidth,
                originalHeight,
                resizedWidth,
                resizedHeight,
                (float)resizedWidth / originalWidth,
                (float)resizedHeight / originalHeight);
        }

        internal static (int Width, int Height) GetPreprocessSize(int width, int height) {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));

            float scale = (float)InputSize / Math.Max(width, height);
            int resizedWidth = Math.Clamp((int)(width * scale + 0.5f), 1, InputSize);
            int resizedHeight = Math.Clamp((int)(height * scale + 0.5f), 1, InputSize);
            return (resizedWidth, resizedHeight);
        }
    }
}
