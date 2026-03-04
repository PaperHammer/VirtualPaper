using Windows.Foundation;

namespace VirtualPaper.Common.Extensions {
    public static class RectExtensions {
        public static Rect IntersectRect(this Rect x, Rect y) {
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

        public static Size GetSize(this Rect rect) => new(rect.Width, rect.Height);

        public static Rect UnionRect(this Rect rect1, Rect rect2) {
            if (rect1.IsEmpty) return rect2;
            if (rect2.IsEmpty) return rect1;

            double left = Math.Min(rect1.Left, rect2.Left);
            double top = Math.Min(rect1.Top, rect2.Top);
            double right = Math.Max(rect1.Right, rect2.Right);
            double bottom = Math.Max(rect1.Bottom, rect2.Bottom);

            return new Rect(left, top, right - left, bottom - top);
        }

        /// <summary>
        /// 向外包裹取整（左上角 Floor，右下角 Ceiling）
        /// 将浮点型 Rect 转换为完全包裹该区域的像素级整型边界
        /// </summary>
        /// <returns>返回 (X, Y, Width, Height) 的整型元组</returns>
        public static Rect RoundOutwardAsInt(this Rect rect) {
            // 左上角坐标向下取整
            int x = (int)Math.Floor(rect.X);
            int y = (int)Math.Floor(rect.Y);

            // 右下角坐标向上取整
            int right = (int)Math.Ceiling(rect.Right);
            int bottom = (int)Math.Ceiling(rect.Bottom);

            // 重新计算宽高，并确保不为负数
            int width = Math.Max(0, right - x);
            int height = Math.Max(0, bottom - y);

            return new Rect(x, y, width, height);
        }
    }
}
