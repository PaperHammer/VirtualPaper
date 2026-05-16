using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Common.Utils.Containers;

namespace VirtualPaper.UIComponent.Converters {
    public partial class StringToImageSourceConverter : IValueConverter {
        public object? Convert(object value, Type targetType, object parameter, string language) {
            if (value is string path && !string.IsNullOrEmpty(path)) {
                try {
                    // 尝试从缓存获取
                    var cachedImage = _imageCache.Get(path);
                    if (cachedImage != null) {
                        return cachedImage;
                    }

                    // 创建新实例并缓存
                    var bitmapImage = new BitmapImage(new Uri(path));
                    _imageCache.Set(path, bitmapImage);
                    return bitmapImage;
                }
                catch {
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }

        private static readonly StringKeyWeakCache<BitmapImage> _imageCache = new();
    }
}
