using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace VirtualPaper.UIComponent.Converters {
    public partial class ImgKeyConverters : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value is string imageKey) {
                string imagePath = $"ms-appx:///assets/{imageKey}";

                try {
                    var image = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                    return image;
                }
                catch (Exception) {
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
