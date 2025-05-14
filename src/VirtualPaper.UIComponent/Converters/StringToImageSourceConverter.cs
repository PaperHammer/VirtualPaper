using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace VirtualPaper.UIComponent.Converters {
    public partial class StringToImageSourceConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            try {
                return value is string path && !string.IsNullOrEmpty(path) ?
                    new BitmapImage(new Uri(path)) :
                    null;
            }
            catch {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
