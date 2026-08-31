namespace VirtualPaper.ML.ObjectDetection.Models {
    public sealed record DetectedObject(
        int LabelId,
        string Label,
        float Score,
        float Left,
        float Top,
        float Right,
        float Bottom) {

        public float Width => Right - Left;
        public float Height => Bottom - Top;
    }
}
