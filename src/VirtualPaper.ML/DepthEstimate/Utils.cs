namespace VirtualPaper.ML.DepthEstimate {
    public static class Utils {
        public static class Fields {
            public static string ModelName { get; } = "model-small.onnx";
            public static string DepthAnythingV2ModelName { get; } =
                "depth_anything_v2_vits_dynamic.onnx";
            public static string OutputFileName { get; } = "depth_img.jpg";
            public static string DepthAnythingV2OutputFileName { get; } =
                "depth_anything_v2.png";
        }
    }
}
