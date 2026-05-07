using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace VirtualPaper.UIComponent.Converters {
    public partial class ResourceKeyToImageConverter : IValueConverter {
        public object? Convert(object value, Type targetType, object parameter, string language) {
            if (value is string resourceKey && !string.IsNullOrEmpty(resourceKey)) {
                if (Application.Current.Resources.TryGetValue(resourceKey, out var resource)) {
                    return resource as BitmapImage;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
