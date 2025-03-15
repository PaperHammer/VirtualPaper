using System;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Converters {
    public partial class DecimalToNumber : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value is double d) {
                return Math.Round(d, 2) * 100;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            if (value is double d) {
                return d / 100;
            }
            return value;
        }
    }
}
