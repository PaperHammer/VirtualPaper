using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Common.Utils.Containers;

namespace VirtualPaper.UIComponent.Converters {    
    public partial class ResourceKeyToImageConverter : IValueConverter {
        public object? Convert(object value, Type targetType, object parameter, string language) {
            if (value is string resourceKey && !string.IsNullOrEmpty(resourceKey)) {

                // 尝试从缓存获取
                var cachedImage = _imageCache.Get(resourceKey);
                if (cachedImage != null) {
                    return cachedImage;
                }

                // 从应用资源加载
                if (Application.Current.Resources.TryGetValue(resourceKey, out var resource) &&
                    resource is BitmapImage bitmapImage) {

                    _imageCache.Set(resourceKey, bitmapImage);
                    return bitmapImage;
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
