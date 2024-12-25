using System.Drawing;
using Microsoft.UI.Windowing;
using VirtualPaper.Common.Utils.PInvoke;
using WinUIEx;

namespace VirtualPaper.PlayerWeb.Utils {
    internal class RawInput {
        internal static Point GetMousePos() {
            if (!Native.GetCursorPos(out Native.POINT P)) {
                return Point.Empty;
            }

            return new Point(P.X, P.Y);
        }

        internal static Native.RECT GetWindowRECT(WindowEx windowEx) {
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
