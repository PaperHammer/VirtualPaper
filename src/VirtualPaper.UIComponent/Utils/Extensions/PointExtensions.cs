using System;
using Windows.Foundation;

namespace VirtualPaper.UIComponent.Utils.Extensions {
    public static class PointExtensions {
        public static double DistanceTo(this Point p1, Point p2) {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static Point Subtract(this Point p1, Point p2) {
            return new Point(p1.X - p2.X, p1.Y - p2.Y);
        }
    }
}
