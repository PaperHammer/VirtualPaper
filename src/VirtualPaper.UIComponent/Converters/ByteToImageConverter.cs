using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace VirtualPaper.UIComponent.Converters {
    public partial class ByteToImageConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            try {
                if (value is not byte[] bytes || bytes.Length == 0)
                    return GetDefaultImage(parameter);

                using var stream = new InMemoryRandomAccessStream();
                stream.WriteAsync(bytes.AsBuffer()).AsTask().Wait();
                stream.Seek(0);

                var bitmapImage = new BitmapImage();
                bitmapImage.SetSource(stream);

                return bitmapImage;
            }
            catch (Exception) {
                return null;
            }
        }

        private static BitmapImage GetDefaultImage(object parameter) {
            return new BitmapImage(new Uri(parameter as string ?? "ms-appx:///Assets/account/default_user_avatar.png"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
