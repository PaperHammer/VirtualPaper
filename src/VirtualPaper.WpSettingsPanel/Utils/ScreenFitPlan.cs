using System;

namespace VirtualPaper.WpSettingsPanel.Utils {
    public sealed record ScreenFitPlan(double Scale, double VisibleWidth, double VisibleHeight,
        int OutputWidth, int OutputHeight) {
        public bool NeedsEnhancement => Scale > 1;

        public static ScreenFitPlan Calculate(int imageWidth, int imageHeight, int screenWidth, int screenHeight, bool cover) {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(screenWidth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(screenHeight);
            double horizontal = (double)screenWidth / imageWidth;
            double vertical = (double)screenHeight / imageHeight;
            double scale = cover ? Math.Max(horizontal, vertical) : Math.Min(horizontal, vertical);
            // The existing model is x4. Keep the source aspect ratio and avoid enlarging beyond its output.
            double enhancement = Math.Clamp(scale, 1, 4);
            return new ScreenFitPlan(scale, Math.Min(imageWidth, screenWidth / scale),
                Math.Min(imageHeight, screenHeight / scale),
                checked((int)Math.Ceiling(imageWidth * enhancement)),
                checked((int)Math.Ceiling(imageHeight * enhancement)));
        }
    }
}
