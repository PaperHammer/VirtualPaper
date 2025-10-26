using System;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Converters {
    public partial class IsStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            bool invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;
            bool isString = value is string or char;
            return invert ? !isString : isString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
