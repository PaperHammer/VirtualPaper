namespace VirtualPaper.ML.Segmentation {
    public static class Utils {
        public static class Fields {
            public static string EncoderModelName { get; } = "mobile_sam_encoder_fp32.onnx";
            public static string DecoderModelName { get; } = "mobile_sam_decoder_fp32.onnx";
        }
    }
}
