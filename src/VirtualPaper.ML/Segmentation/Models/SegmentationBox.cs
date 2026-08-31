using VirtualPaper.ML.ObjectDetection.Models;

namespace VirtualPaper.ML.Segmentation.Models {
    public sealed record SegmentationBox(
        float Left,
        float Top,
        float Right,
        float Bottom) {

        public float Width => Right - Left;
        public float Height => Bottom - Top;

        public static SegmentationBox FromDetection(DetectedObject detection) {
            ArgumentNullException.ThrowIfNull(detection);
            return new SegmentationBox(
                detection.Left,
                detection.Top,
                detection.Right,
                detection.Bottom);
        }

        internal SegmentationBox ClampAndValidate(int imageWidth, int imageHeight) {
            if (!float.IsFinite(Left) || !float.IsFinite(Top) ||
                !float.IsFinite(Right) || !float.IsFinite(Bottom)) {
                throw new ArgumentOutOfRangeException(
                    nameof(SegmentationBox),
                    "Box coordinates must be finite.");
            }

            float left = Math.Clamp(Left, 0f, imageWidth);
            float top = Math.Clamp(Top, 0f, imageHeight);
            float right = Math.Clamp(Right, 0f, imageWidth);
            float bottom = Math.Clamp(Bottom, 0f, imageHeight);

            if (right <= left || bottom <= top)
                throw new ArgumentOutOfRangeException(
                    nameof(SegmentationBox),
                    "Box must have a positive area after clipping to the image.");

            return new SegmentationBox(left, top, right, bottom);
        }
    }
}
