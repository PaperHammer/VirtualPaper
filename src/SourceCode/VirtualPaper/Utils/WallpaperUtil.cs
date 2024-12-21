using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files.Models;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using Size = OpenCvSharp.Size;

namespace VirtualPaper.Utils {
    public static class WallpaperUtil {
        internal static IWpBasicData GetWpBasicDataByForlderPath(string folderPath) {
            WpBasicData data = new();
            string basicDataFilePath = Path.Combine(folderPath, Constants.Field.WpBasicDataFileName);
            if (File.Exists(basicDataFilePath)) {
                data = JsonStorage<WpBasicData>.LoadData(basicDataFilePath)
                    ?? throw new Exception("Corrupted wallpaper bacis-data");
            }

            return data;
        }

        internal static IWpMetadata GetWallpaperByFolder(string folderPath, string monitorContent, string rtype) {
            WpMetadata data = new();
            string basicDataFilePath = Path.Combine(folderPath, Constants.Field.WpBasicDataFileName);
            if (File.Exists(basicDataFilePath)) {
                data.BasicData = JsonStorage<WpBasicData>.LoadData(basicDataFilePath)
                    ?? throw new Exception("Corrupted wallpaper bacis-data");
            }

            string runtimeDataFilePath = Path.Combine(folderPath, monitorContent, rtype, Constants.Field.WpRuntimeDataFileName);
            if (File.Exists(runtimeDataFilePath)) {
                data.RuntimeData = JsonStorage<WpRuntimeData>.LoadData(runtimeDataFilePath)
                    ?? throw new Exception("Corrupted wallpaper runtime-data");
            }

            return data;
        }

        internal static string CreateWpEffectFileTemplate(
            string folderPath,
            RuntimeType rtype) {
            string wpEffectFilePathTemplate = Path.Combine(folderPath, Constants.Field.WpEffectFilePathTemplate);
            if (!File.Exists(wpEffectFilePathTemplate)) {
                File.Create(wpEffectFilePathTemplate).Close();
            }

            switch (rtype) {
                case RuntimeType.RImage:
                    PictureAndGifCostumise pictureAndGifCostumize = new();
                    JsonStorage<PictureAndGifCostumise>.StoreData(wpEffectFilePathTemplate, pictureAndGifCostumize);
                    break;
                case RuntimeType.RImage3D:
                    Picture3DCostumize picture3DCostumize = new();
                    JsonStorage<Picture3DCostumize>.StoreData(wpEffectFilePathTemplate, picture3DCostumize);
                    break;
                case RuntimeType.RVideo:
                    VideoCostumize videoCostumize = new();
                    JsonStorage<VideoCostumize>.StoreData(wpEffectFilePathTemplate, videoCostumize);
                    break;
                default:
                    break;
            }

            return wpEffectFilePathTemplate;
        }

        //internal static string CreateWpEffectFileTemporary(
        //    string folderPath,
        //    string wpEffectFilePathTemplate) {
        //    string wpEffectFilePathTemporary = Path.Combine(folderPath, Constants.Field.WpEffectFilePathTemporary);
        //    File.Copy(wpEffectFilePathTemplate, wpEffectFilePathTemporary, true);

        //    return wpEffectFilePathTemporary;
        //}

        internal static string CreateWpEffectFileUsingOrTemporary(
            int type,
            string folderPath,
            string wpEffectFilePathTemplate,
            string monitorContent,
            RuntimeType rtype,
            WallpaperArrangement arrangement) {
            string filePath = string.Empty;
            if (wpEffectFilePathTemplate != null) {
                if (monitorContent != null) {
                    string wpdataUsingFolder = string.Empty;
                    switch (arrangement) {
                        case WallpaperArrangement.Per:
                            wpdataUsingFolder = Path.Combine(folderPath, monitorContent, rtype.ToString());
                            break;
                        case WallpaperArrangement.Expand:
                            wpdataUsingFolder = Path.Combine(folderPath, "Expand", rtype.ToString());
                            break;
                        case WallpaperArrangement.Duplicate:
                            wpdataUsingFolder = Path.Combine(folderPath, "Duplicate", rtype.ToString());
                            break;
                    }
                    Directory.CreateDirectory(wpdataUsingFolder);
                    filePath = Path.Combine(wpdataUsingFolder, type == 0 ? Constants.Field.WpEffectFilePathUsing : Constants.Field.WpEffectFilePathTemporary);
                    File.Copy(wpEffectFilePathTemplate, filePath, true);
                }
            }

            return filePath;
        }

        internal static void CreateGif(string filePath, string coverFilePath, FileType ftype, CancellationToken token) {
            GifBitmapEncoder gEnc = new();
            if (ftype == FileType.FPicture) {
                Bitmap bitmap = new(filePath);
                var src = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                gEnc.Frames.Add(BitmapFrame.Create(src));
                bitmap.Dispose();
            }
            else if (ftype == FileType.FVideo || ftype == FileType.FGif) {
                using var cap = new VideoCapture(filePath);
                if (!cap.IsOpened()) {
                    throw new Exception("An Error occoured");
                }

                int frameCnt = cap.FrameCount;
                int frameLimit = Math.Min(frameCnt, 60);

                for (int i = 0; i < frameLimit && !token.IsCancellationRequested; i++) {
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

                if (token.IsCancellationRequested) {
                    throw new OperationCanceledException("The video frame reading was canceled.");
                }
            }

            using var ms = new MemoryStream();
            gEnc.Save(ms);
            var fileBytes = ms.ToArray();
            // This is the NETSCAPE2.0 Application Extension.
            // 创建循环动画
            var applicationExtension = new byte[] { 33, 255, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0 };
            var newBytes = new List<byte>();
            newBytes.AddRange(fileBytes.Take(13));
            newBytes.AddRange(applicationExtension);
            newBytes.AddRange(fileBytes.Skip(13));
            File.WriteAllBytes(coverFilePath, [.. newBytes]);
        }

        internal static FileProperty GetWpProperty(string filePath, FileType ftype) {
            FileProperty fileProperty = new() {
                FileExtension = Path.GetExtension(filePath)
            };

            FileInfo fileInfo = new(filePath);
            double size = double.Parse((fileInfo.Length / 1024.0 / 1024.0).ToString("0.00"));
            if (size == 0) fileProperty.FileSize = (fileInfo.Length / 1024.0).ToString("0.00") + " KB";
            else fileProperty.FileSize = (fileInfo.Length / 1024.0 / 1024.0).ToString("0.00") + " MB";

            switch (ftype) {
                case FileType.FVideo or FileType.FGif: {
                        using var capture = new VideoCapture(filePath);
                        if (!capture.IsOpened()) throw new();

                        var fps = capture.Fps;
                        int width = capture.FrameWidth;
                        int height = capture.FrameHeight;
                        double ratio = (double)width / height;

                        fileProperty.Resolution = $"{width} * {height} ({fps:0.00} fps)";
                        fileProperty.AspectRatio = GetRatio(ratio);

                        capture.Release();

                        break;
                    }
                case FileType.FPicture: {
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

            return fileProperty;
        }

        private static string GetRatio(double aspectRatio) {
            if (Math.Abs(aspectRatio - 1.6) < 0.01) {
                return "16:10";
            }
            else if (Math.Abs(aspectRatio - 1.7778) < 0.01) {
                return "16:9";
            }
            else if (Math.Abs(aspectRatio - 1.3333) < 0.01) {
                return "4:3";
            }
            return "FUnknown";
        }
    }
}
