namespace VirtualPaper.ML.ObjectDetection {
    public static class Utils {
        public static class Fields {
            public static string ModelName { get; } = "rtmdet_tiny_dynamic_fp32.onnx";
        }

        public static IReadOnlyList<string> CocoLabels { get; } = new[] {
            "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
            "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
            "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
            "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
            "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
            "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
            "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven",
            "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
        };

        public static string GetLabelName(int labelId) =>
            labelId >= 0 && labelId < CocoLabels.Count
                ? CocoLabels[labelId]
                : $"class_{labelId}";
    }
}
