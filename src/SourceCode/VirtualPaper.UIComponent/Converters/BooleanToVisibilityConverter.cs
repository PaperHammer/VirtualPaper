using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Converters {
    public partial class BooleanToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value is bool) {
                return (bool)value ^ (parameter as string ?? string.Empty).Equals("Reverse") ?
                    Visibility.Visible : Visibility.Collapsed;
            }
            else {
                return value is not null ^ (parameter as string ?? string.Empty).Equals("Reverse") ?
                    Visibility.Visible : Visibility.Collapsed;
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
