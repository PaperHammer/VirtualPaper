using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace VirtualPaper.DraftPanel.Utils {
    internal static class TypeConvertUtil {
        internal static Brush Hex2Brush(uint color) {
            var a = (byte)(color >> 24);
            var r = (byte)(color >> 16);
            var g = (byte)(color >> 8);
            var b = (byte)color;

            Color clr = Color.FromArgb(a, r, g, b);
            return new SolidColorBrush(clr);
        }

        internal static uint Color2Hex(Color color) {
            uint a = color.A;
            uint r = color.R;
            uint g = color.G;
            uint b = color.B;
            
            return (a << 24) | (r << 16) | (g << 8) | b;
        }
    }
}
