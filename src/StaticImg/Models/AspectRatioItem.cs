namespace Workloads.Creation.StaticImg.Models {
    public class AspectRatioItem {
        public string DisplayText { get; }
        public double BorderWidth { get; }
        public double BorderHeight { get; }
        public double Ratio { get; }

        public AspectRatioItem(string displayText, double borderWidth, double borderHeight) {
            DisplayText = displayText;
            BorderWidth = borderWidth;
            BorderHeight = borderHeight;
            Ratio = ParseRatio(displayText);
        }

        private static double ParseRatio(string text) {
            var parts = text.Split(':');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out var x) &&
                double.TryParse(parts[1], out var y))
                return x / y;
            return 0; // 0表示自由比例
        }
    }
}
