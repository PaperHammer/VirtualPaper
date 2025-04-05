using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Converters {
    public partial class NullToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
