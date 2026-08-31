namespace VirtualPaper.ML.DepthEstimate.Models {
    public enum DepthAnythingResizeMode {
        /// <summary>
        /// Fits the longest image side within InputSize. Preserves geometry and
        /// uses the least CPU time for wide or tall images.
        /// </summary>
        FitLongestSide,

        /// <summary>
        /// Makes the shortest image side at least InputSize. This matches the
        /// official Depth Anything V2 lower-bound preprocessing and costs more.
        /// </summary>
        FillShortestSide,

        /// <summary>
        /// Resizes directly to an InputSize square, matching the ONNX release's
        /// simple inference example but changing the source aspect ratio.
        /// </summary>
        StretchSquare
    }
}
