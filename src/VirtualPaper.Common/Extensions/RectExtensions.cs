using Windows.Foundation;

namespace VirtualPaper.Common.Extensions {
    public static class RectExtensions {
        public static Rect GetIntersect(this Rect x, Rect y) {
            if (x.IsEmpty || y.IsEmpty)
                return Rect.Empty;
            double x1 = Math.Max(x.X, y.X);
            double y1 = Math.Max(x.Y, y.Y);
            double x2 = Math.Min(x.X + x.Width, y.X + y.Width);
            double y2 = Math.Min(x.Y + x.Height, y.Y + y.Height);
            if (x1 < x2 && y1 < y2)
                return new Rect(x1, y1, x2 - x1, y2 - y1);
            return Rect.Empty;
        }
    }
}
