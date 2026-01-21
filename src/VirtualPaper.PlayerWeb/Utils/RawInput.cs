using VirtualPaper.Common.Utils.PInvoke;

namespace VirtualPaper.PlayerWeb.Utils {
    internal class RawInput {
        internal static Point GetMousePos() {
            if (!Native.GetCursorPos(out Native.POINT pos)) {
                return Point.Empty;
            }

            // 本地化游标值
            return new Point(pos.X, pos.Y);
        }
    }
}
