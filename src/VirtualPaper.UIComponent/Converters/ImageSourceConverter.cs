using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace VirtualPaper.UIComponent.Converters {
    public partial class ImageSourceConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value == null || value is not string imagePath) {
                return null; // 或者返回一个默认的占位符图片
            }

            try {
                var uri = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                var bitmapImage = new BitmapImage(uri) {
                    DecodePixelWidth = 200
                };

                //// 强制立即加载图片资源，避免首次显示延迟
                //bitmapImage.ImageOpened += (sender, e) =>
                //{
                //    // 图片成功加载后触发的操作（可选）
                //};
                bitmapImage.ImageFailed += (sender, e) =>
                {
                    // 图片加载失败后的处理（例如，使用默认图片）
                    System.Diagnostics.Debug.WriteLine("Image loading failed.");
                };

                return bitmapImage;
            }
            catch (Exception ex) {
                // 处理异常情况，例如路径无效等情况
                System.Diagnostics.Debug.WriteLine($"Image loading failed: {ex.Message}");
                return null; // 或者返回一个默认的占位符图片
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
