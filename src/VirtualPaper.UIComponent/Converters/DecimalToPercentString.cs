using System;
using System.Diagnostics;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Converters {
    public partial class DecimalToPercentString : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value is double d) {
                Debug.WriteLine(d);
                return $"{Math.Round(Math.Round(d, 3) * 100, 1)}%";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
