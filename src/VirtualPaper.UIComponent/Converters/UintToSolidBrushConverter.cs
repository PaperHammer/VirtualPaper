using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace VirtualPaper.UIComponent.Converters {
    public partial class UintToSolidBrushConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            return HexToSolidBrush(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            return SolidBrushToHex(value);
        }

        public static uint SolidBrushToHex(object value) {
            try {
                if (value is SolidColorBrush brush && brush.Color is Color color) {
                    return ColorToHex(color);
                }
            }
            catch (Exception) {
            }

            return 0x00FFFFFF; // ARGB 格式的透明白色
        }

        public static uint ColorToHex(Color color) {
            try {
                uint a = color.A;
                uint r = color.R;
                uint g = color.G;
                uint b = color.B;

                return (a << 24) | (r << 16) | (g << 8) | b;
            }
            catch (Exception) {
            }

            return 0x00FFFFFF; // ARGB 格式的透明白色
        }

        public static SolidColorBrush HexToSolidBrush(object value) {
            try {
                if (value is uint color) {
                    var a = (byte)(color >> 24);
                    var r = (byte)(color >> 16);
                    var g = (byte)(color >> 8);
                    var b = (byte)color;

                    Color clr = Color.FromArgb(a, r, g, b);
                    return new SolidColorBrush(clr);
                }
            }
            catch (Exception) {
            }

            // 默认返回白色画刷
            return new SolidColorBrush(Colors.White);
        }
    }
}
