using System;
using System.Threading.Tasks;
using VirtualPaper.Common.Logging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Workloads.Creation.StaticImg.Utils {
    public static class ImgUtils {
        public static async Task<(uint Width, uint Height)> GetImagePixelSizeAsync(string filePath) {
            try {
                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                using IRandomAccessStream stream = await file.OpenReadAsync();
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                return (decoder.PixelWidth, decoder.PixelHeight);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error($"读取图片尺寸失败, 路径: {filePath}, 错误: {ex.Message}");
                return (0, 0);
            }
        }
    }
}
