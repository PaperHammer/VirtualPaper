using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Common.Utils.Archive;

namespace Workloads.Creation.StaticImg.Utils {
    static class WriteableBitmapExtension {
        public static async Task<WriteableBitmap> DeepCopyAsync(this WriteableBitmap source) {
            ArgumentNullException.ThrowIfNull(source);

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            WriteableBitmap copy = new(width, height);

            using (var sourceStream = source.PixelBuffer.AsStream())
            using (var targetStream = copy.PixelBuffer.AsStream()) {
                await sourceStream.CopyToAsync(targetStream);
            }

            return copy;
        }

        public static async Task<byte[]> WriteableBitmapToBinaryAsync(WriteableBitmap bitmap) {
            ArgumentNullException.ThrowIfNull(bitmap);

            // 获取像素缓冲区
            using (var stream = bitmap.PixelBuffer.AsStream()) {
                byte[] pixelData = new byte[stream.Length];
                await stream.ReadAsync(pixelData);

                // 创建二进制流
                using (var memoryStream = new MemoryStream())
                using (var writer = new BinaryWriter(memoryStream)) {
                    // 写入宽度和高度
                    writer.Write(bitmap.PixelWidth);
                    writer.Write(bitmap.PixelHeight);

                    // 写入像素数据
                    writer.Write(pixelData);

                    // 获取未压缩的二进制数据
                    byte[] uncompressedData = memoryStream.ToArray();

                    // 压缩二进制数据
                    return await ZipUtil.CompressAsync(uncompressedData);
                }
            }
        }

        public static async Task<WriteableBitmap> BinaryToWriteableBitmapAsync(byte[] binaryData) {
            ArgumentNullException.ThrowIfNull(binaryData);

            // 解压缩二进制数据
            byte[] decompressedData = await ZipUtil.DecompressAsync(binaryData);

            // 读取解压缩后的数据
            using (var memoryStream = new MemoryStream(decompressedData))
            using (var reader = new BinaryReader(memoryStream)) {
                // 读取宽度和高度
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();

                // 读取像素数据
                byte[] pixelData = reader.ReadBytes((int)(memoryStream.Length - memoryStream.Position));

                // 创建 WriteableBitmap
                WriteableBitmap bitmap = new(width, height);
                using (var bitmapStream = bitmap.PixelBuffer.AsStream()) {
                    await bitmapStream.WriteAsync(pixelData);
                }

                return bitmap;
            }
        }

        public static async Task WriteableBitmapToFileAsync(WriteableBitmap bitmap, string filePath) {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            // 获取字节数组
            byte[] binaryData = await WriteableBitmapToBinaryAsync(bitmap);
            // 将字节数组写入文件
            using (var fileStream = File.Create(filePath)) {
                await fileStream.WriteAsync(binaryData);
            }
        }

        public static async Task<WriteableBitmap> ReadWriteableBitmapFromFileAsync(string filePath) {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found.", filePath);

            // 从文件中读取字节数组
            byte[] binaryData = await File.ReadAllBytesAsync(filePath);
            var bitmap = await BinaryToWriteableBitmapAsync(binaryData);

            return bitmap;
        }


        public static async Task FillWithWhiteAsync(WriteableBitmap bitmap) {
            ArgumentNullException.ThrowIfNull(bitmap);

            // 获取像素缓冲区
            using (var stream = bitmap.PixelBuffer.AsStream()) {
                // 计算总像素数
                int pixelCount = bitmap.PixelWidth * bitmap.PixelHeight;

                // 创建一个字节数组，每个像素占用 4 字节（BGRA 格式）
                byte[] whitePixels = new byte[pixelCount * 4];

                // 使用 Array.Fill 快速填充白色像素
                for (int i = 0; i < whitePixels.Length; i += 4) {
                    whitePixels[i] = 255;     // Blue
                    whitePixels[i + 1] = 255; // Green
                    whitePixels[i + 2] = 255; // Red
                    whitePixels[i + 3] = 255; // Alpha
                }

                // 写入像素数据到缓冲区
                await stream.WriteAsync(whitePixels);
            }
        }
    }
}
