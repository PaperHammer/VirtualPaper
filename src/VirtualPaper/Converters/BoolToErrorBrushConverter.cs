using System.Globalization;
using System.Windows.Data;

namespace VirtualPaper.Converters {
    public class BoolToErrorBrushConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is bool isError && isError) {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0x11, 0x23)); // Error red
            }
            return System.Windows.Application.Current.Resources["AccentFillColorDefaultBrush"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
