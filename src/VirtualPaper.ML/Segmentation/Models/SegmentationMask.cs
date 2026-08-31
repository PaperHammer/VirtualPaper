namespace VirtualPaper.ML.Segmentation.Models {
    public sealed record SegmentationMask(
        SegmentationBox SourceBox,
        float PredictedIoU,
        byte[] Alpha,
        int Width,
        int Height) {

        public byte GetAlpha(int x, int y) {
            if ((uint)x >= (uint)Width)
                throw new ArgumentOutOfRangeException(nameof(x));
            if ((uint)y >= (uint)Height)
                throw new ArgumentOutOfRangeException(nameof(y));

            return Alpha[checked(y * Width + x)];
        }
    }
}
