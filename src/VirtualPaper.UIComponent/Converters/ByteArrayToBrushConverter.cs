using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace VirtualPaper.UIComponent.Converters {
    public partial class ByteArrayToBrushConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value is byte[] argb && argb.Length == 4) {
                var color = Color.FromArgb(argb[0], argb[1], argb[2], argb[3]);
                return new SolidColorBrush(color);
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
