using System.Drawing;
using Microsoft.UI.Windowing;
using VirtualPaper.Common.Utils.PInvoke;
using WinUIEx;

namespace VirtualPaper.PlayerWeb.Utils {
    internal class RawInput {
        internal static Point GetMousePos() {
            if (!Native.GetCursorPos(out Native.POINT pos)) {
                return Point.Empty;
            }

            // 本地化游标值
            return new Point(pos.X, pos.Y);
        }

        internal static Native.RECT GetWindowRECT(WindowEx windowEx) {
            if (windowEx == null || windowEx.AppWindow == null) {
                return default;
            }

            AppWindow appWindow = windowEx.AppWindow;

            Native.RECT rect = new() {
                Left = appWindow.Position.X,
                Top = appWindow.Position.Y,
                Right = appWindow.Position.X + appWindow.Size.Width,
                Bottom = appWindow.Position.Y + appWindow.Size.Height,
            };

            return rect;
        }
    }
}
