using System.Runtime.InteropServices;
using OpenCvSharp;
using VirtualPaper.ML.Segmentation.Models;

namespace VirtualPaper.ML.Segmentation {
    /// <summary>
    /// Persists MobileSAM results for inspection and for later layer composition.
    /// </summary>
    public static class SegmentationVisualization {
        public static void SaveAlphaMask(SegmentationMask mask, string outputPath) {
            ValidateMask(mask);
            PrepareOutputPath(outputPath);

            using Mat alpha = CreateAlphaMat(mask);
            if (!Cv2.ImWrite(outputPath, alpha))
                throw new IOException($"Failed to save segmentation mask: {outputPath}");
        }

        public static void SaveTransparentCutout(
            string imagePath,
            SegmentationMask mask,
            string outputPath) {

            ValidateMask(mask);
            ValidateInputImagePath(imagePath);

            if (!string.Equals(Path.GetExtension(outputPath), ".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "Transparent cutouts must use the .png extension.",
                    nameof(outputPath));

            PrepareOutputPath(outputPath);
            using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
            ValidateImageDimensions(image, mask, imagePath);
            using Mat alpha = CreateAlphaMat(mask);

            Mat[] bgrChannels = Cv2.Split(image);
            try {
                using var bgra = new Mat();
                Cv2.Merge([bgrChannels[0], bgrChannels[1], bgrChannels[2], alpha], bgra);
                if (!Cv2.ImWrite(outputPath, bgra))
                    throw new IOException($"Failed to save transparent cutout: {outputPath}");
            }
            finally {
                foreach (Mat channel in bgrChannels)
                    channel.Dispose();
            }
        }

        public static void SaveContourPreview(
            string imagePath,
            SegmentationMask mask,
            string outputPath,
            Scalar? color = null,
            int thickness = 2) {

            ValidateMask(mask);
            ValidateInputImagePath(imagePath);
            if (thickness <= 0)
                throw new ArgumentOutOfRangeException(nameof(thickness));

            PrepareOutputPath(outputPath);
            using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
            ValidateImageDimensions(image, mask, imagePath);
            using Mat alpha = CreateAlphaMat(mask);
            using Mat contourInput = alpha.Clone();

            Cv2.FindContours(
                contourInput,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            Cv2.DrawContours(
                image,
                contours,
                -1,
                color ?? new Scalar(0, 255, 0),
                thickness,
                LineTypes.AntiAlias);

            if (!Cv2.ImWrite(outputPath, image))
                throw new IOException($"Failed to save segmentation preview: {outputPath}");
        }

        private static Mat CreateAlphaMat(SegmentationMask mask) {
            var alpha = new Mat(mask.Height, mask.Width, MatType.CV_8UC1);
            Marshal.Copy(mask.Alpha, 0, alpha.Data, mask.Alpha.Length);
            return alpha;
        }

        private static void ValidateMask(SegmentationMask mask) {
            ArgumentNullException.ThrowIfNull(mask);
            if (mask.Width <= 0 || mask.Height <= 0)
                throw new ArgumentException("Mask dimensions must be positive.", nameof(mask));
            if (mask.Alpha is null || mask.Alpha.Length != checked(mask.Width * mask.Height))
                throw new ArgumentException(
                    "Alpha buffer length must equal mask width multiplied by height.",
                    nameof(mask));
        }

        private static void ValidateInputImagePath(string imagePath) {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException($"Input image not found: {imagePath}", imagePath);
        }

        private static void ValidateImageDimensions(Mat image, SegmentationMask mask, string imagePath) {
            if (image.Empty())
                throw new ArgumentException($"Failed to load image: {imagePath}", nameof(imagePath));
            if (image.Width != mask.Width || image.Height != mask.Height) {
                throw new ArgumentException(
                    $"Image size {image.Width}x{image.Height} does not match mask size " +
                    $"{mask.Width}x{mask.Height}.",
                    nameof(mask));
            }
        }

        private static void PrepareOutputPath(string outputPath) {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path cannot be empty.", nameof(outputPath));

            string fullPath = Path.GetFullPath(outputPath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (directory is not null)
                Directory.CreateDirectory(directory);
        }
    }
}
