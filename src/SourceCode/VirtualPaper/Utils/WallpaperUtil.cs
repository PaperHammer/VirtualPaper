using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files.Models;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.WallpaperMetaData;
using Size = OpenCvSharp.Size;

namespace VirtualPaper.Utils
{
    public static class WallpaperUtil
    {
        public static IMetaData ScanWallpaperFolder(string folderPath)
        {
            if (File.Exists(Path.Combine(folderPath, "MetaData.json")))
            {
                MetaData metaData = JsonStorage<MetaData>.LoadData(Path.Combine(folderPath, "MetaData.json"));
                
                return metaData ?? throw new Exception("Corrupted wallpaper metadata");
            }
            throw new Exception("Wallpaper not found.");
        }

        public static void CreateCustomizeFile(string wpCustomizePath, WallpaperType type)
        {
            if (type == WallpaperType.picture || type == WallpaperType.gif)
            {
                PictureCostumise pictureCostumize = new();
                JsonStorage<PictureCostumise>.StoreData(wpCustomizePath, pictureCostumize);
            }
            else if (type == WallpaperType.video)
            {
                VideoAndGifCostumize videoAndGifCostumize = new();
                JsonStorage<VideoAndGifCostumize>.StoreData(wpCustomizePath, videoAndGifCostumize);
            }
        }

        public static void TryCreateGif(string filePath, string thumbnailPath, WallpaperType type, CancellationToken token)
        {
            GifBitmapEncoder gEnc = new();
            if (type == WallpaperType.picture)
            {
                Bitmap bitmap = new(filePath);
                var src = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                gEnc.Frames.Add(BitmapFrame.Create(src));
                bitmap.Dispose();
            }
            else if (type == WallpaperType.video || type == WallpaperType.gif)
            {
                using var cap = new VideoCapture(filePath);
                if (!cap.IsOpened())
                {
                    throw new Exception("An Error occoured");
                }

                int frameCnt = cap.FrameCount;
                int frameLimit = Math.Min(frameCnt, 60);

                for (int i = 0; i < frameLimit && !token.IsCancellationRequested; i++)
                {
                    cap.Set(VideoCaptureProperties.PosFrames, i);
                    using Mat frame = new();
                    cap.Read(frame);
                    if (frame.Empty()) break;

                    token.ThrowIfCancellationRequested();

                    Bitmap bitmap = BitmapConverter.ToBitmap(frame);
                    frame.Release();

                    var src = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                    gEnc.Frames.Add(BitmapFrame.Create(src));
                    bitmap.Dispose();
                }

                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException("The video frame reading was canceled.");
                }                
            }

            using (var ms = new MemoryStream())
            {
                gEnc.Save(ms);
                var fileBytes = ms.ToArray();
                // This is the NETSCAPE2.0 Application Extension.
                // 创建循环动画
                var applicationExtension = new byte[] { 33, 255, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0 };
                var newBytes = new List<byte>();
                newBytes.AddRange(fileBytes.Take(13));
                newBytes.AddRange(applicationExtension);
                newBytes.AddRange(fileBytes.Skip(13));
                File.WriteAllBytes(thumbnailPath, [.. newBytes]);
            }
        }

        public static FileProperty TryGetProeprtyInfo(string filePath, WallpaperType type)
        {
            FileProperty fileProperty = new()
            {
                FileExtension = Path.GetExtension(filePath)
            };

            FileInfo fileInfo = new(filePath);
            double size = double.Parse((fileInfo.Length / 1024.0 / 1024.0).ToString("0.00"));
            if (size == 0) fileProperty.FileSize = (fileInfo.Length / 1024.0).ToString("0.00") + " KB";
            else fileProperty.FileSize = (fileInfo.Length / 1024.0 / 1024.0).ToString("0.00") + " MB";

            try
            {
                switch (type)
                {
                    case WallpaperType.video or WallpaperType.gif:
                        {
                            using var capture = new VideoCapture(filePath);
                            if (!capture.IsOpened()) throw new();

                            var fps = capture.Fps;
                            int width = capture.FrameWidth;
                            int height = capture.FrameHeight;
                            double ratio = (double)width / height;

                            fileProperty.Resolution = $"{width} * {height} {fps:0.00}帧";
                            fileProperty.AspectRatio = GetRatio(ratio);

                            capture.Release();

                            break;
                        }
                    case WallpaperType.picture:
                        {
                            using var img = new Mat(filePath);

                            Size sz = img.Size();
                            int width = sz.Width;
                            int height = sz.Height;
                            double ratio = (double)width / height;

                            fileProperty.Resolution = $"{width} * {height}";
                            fileProperty.AspectRatio = GetRatio(ratio);

                            break;
                        }
                }
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"{App.GetResourceDicString("WpUtils_TextFileOpenFailed")} ({Path.GetExtension(filePath)})");
            }

            return fileProperty;
        }

        private static string GetRatio(double aspectRatio)
        {
            if (Math.Abs(aspectRatio - 1.6) < 0.01)
            {
                return "16:10";
            }
            else if (Math.Abs(aspectRatio - 1.7778) < 0.01)
            {
                return "16:9";
            }
            else if (Math.Abs(aspectRatio - 1.3333) < 0.01)
            {
                return "4:3";
            }
            return "Unknown";
        }
    }
}
