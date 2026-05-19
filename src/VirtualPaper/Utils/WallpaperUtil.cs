using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files.Models;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using Size = OpenCvSharp.Size;

namespace VirtualPaper.Utils {
    public static class WallpaperUtil {
        internal static IWpBasicData GetWpBasicDataByForlderPath(string folderPath) {
            WpBasicData data = new();
            string basicDataFilePath = Path.Combine(folderPath, Constants.Field.WpBasicDataFileName);
            if (File.Exists(basicDataFilePath)) {
                data = JsonSaver.Load<WpBasicData>(basicDataFilePath, WpBasicDataContext.Default)
                    ?? throw new Exception("Corrupted wallpaper bacis-_data");
            }

            return data;
        }

        internal static IWpMetadata GetWallpaperByFolder(string folderPath, string monitorContent, string rtype) {
            WpMetadata data = new();
            string basicDataFilePath = Path.Combine(folderPath, Constants.Field.WpBasicDataFileName);
            if (File.Exists(basicDataFilePath)) {
                data.BasicData = JsonSaver.Load<WpBasicData>(basicDataFilePath, WpBasicDataContext.Default)
                    ?? throw new Exception("Corrupted wallpaper bacis-_data");
            }

            string runtimeDataFilePath = Path.Combine(folderPath, monitorContent, rtype, Constants.Field.WpRuntimeDataFileName);
            if (File.Exists(runtimeDataFilePath)) {
                data.RuntimeData = JsonSaver.Load<WpRuntimeData>(runtimeDataFilePath, WpRuntimeDataContext.Default)
                    ?? throw new Exception("Corrupted wallpaper runtime-_data");
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
                    JsonSaver.Save(wpEffectFilePathTemplate, pictureAndGifCostumize, PictureAndGifCostumiseContext.Default);
                    break;
                case RuntimeType.RImage3D:
                    Picture3DCostumize picture3DCostumize = new();
                    JsonSaver.Save(wpEffectFilePathTemplate, picture3DCostumize, Picture3DCostumizeContext.Default);
                    break;
                case RuntimeType.RVideo:
                    VideoCostumize videoCostumize = new();
                    JsonSaver.Save(wpEffectFilePathTemplate, videoCostumize, VideoCostumizeContext.Default);
                    break;
                case RuntimeType.RWeb:
                    WebCostumize webCostumize = new();
                    JsonSaver.Save(wpEffectFilePathTemplate, webCostumize, WebCostumizeContext.Default);
                    break;
                default:
                    break;
            }

            return wpEffectFilePathTemplate;
        }

        internal static string CreateWpEffectFileUsingOrTemporary(
            int type,
            string folderPath,
            string wpEffectFilePathTemplate,
            string monitorContent,
            RuntimeType rtype) {
            string filePath = string.Empty;
            if (wpEffectFilePathTemplate != null) {
                string wpRuntimeDataFolder = Path.Combine(folderPath, monitorContent, rtype.ToString());
                Directory.CreateDirectory(wpRuntimeDataFolder);
                filePath = Path.Combine(wpRuntimeDataFolder, type == 0 ? Constants.Field.WpEffectFilePathUsing : Constants.Field.WpEffectFilePathTemporary);
                File.Copy(wpEffectFilePathTemplate, filePath, true);
            }

            return filePath;
        }

        internal static void CreateGif(string filePath, string thuFilePath, FileType ftype, CancellationToken token) {
            Mat ResizeTo1080p(Mat img) {
                Size newSize = new(Math.Min(img.Cols, 960), Math.Min(img.Rows, 600));
                Mat resizedImg = new();
                Cv2.Resize(img, resizedImg, newSize);

                return resizedImg;
            }

            if (ftype == FileType.FImage) {
                using var bitmap = new Bitmap(filePath);
                using var mat = BitmapConverter.ToMat(bitmap);
                using var resizedMat = ResizeTo1080p(mat);
                using var resizedBitmap = BitmapConverter.ToBitmap(resizedMat);
                resizedBitmap.Save(thuFilePath);
                return;
            }
            else if (ftype == FileType.FGif) {
                using var gifStream = File.OpenRead(filePath);
                var decoder = new GifBitmapDecoder(gifStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                var frames = decoder.Frames.Select(f => {
                    var mat = BitmapConverter.ToMat(BitmapSourceToBitmap(f));
                    return ResizeTo1080p(mat);
                }).ToList();
                SaveGifFrames(frames, thuFilePath);
                return;
            }
            else if (ftype == FileType.FVideo) {
                using var cap = new VideoCapture(filePath);
                if (!cap.IsOpened()) {
                    throw new Exception("Failed to open video file.");
                }

                double fps = Math.Min(cap.Get(VideoCaptureProperties.Fps), 24); // 设置最大帧率为24fps
                int frameLimit = (int)Math.Min(cap.FrameCount, fps * 3); // 最多取3秒的帧数

                List<Mat> frames = [];
                for (int i = 0; i < frameLimit && !token.IsCancellationRequested; i++) {
                    token.ThrowIfCancellationRequested();

                    cap.Set(VideoCaptureProperties.PosFrames, i);
                    using Mat frame = new();
                    cap.Read(frame);
                    if (frame.Empty()) break;

                    using var resizedFrame = ResizeTo1080p(frame);
                    frames.Add(resizedFrame.Clone());
                }

                SaveGifFrames(frames, thuFilePath);
            }

            void SaveGifFrames(List<Mat> frames, string outputPath) {
                GifBitmapEncoder encoder = new();
                foreach (var frame in frames) {
                    token.ThrowIfCancellationRequested();

                    using Bitmap bitmap = BitmapConverter.ToBitmap(frame);
                    var src = Imaging.CreateBitmapSourceFromHBitmap(
                        bitmap.GetHbitmap(),
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    encoder.Frames.Add(BitmapFrame.Create(src));
                }

                using var ms = new MemoryStream();
                encoder.Save(ms);
                var fileBytes = ms.ToArray();
                var newBytes = new List<byte>(fileBytes.Take(13));
                newBytes.AddRange(_applicationExtension);
                newBytes.AddRange(fileBytes.Skip(13));

                File.WriteAllBytes(outputPath, [.. newBytes]);
            }

            Bitmap BitmapSourceToBitmap(BitmapSource s) {
                using Bitmap bmp = new(s.PixelWidth, s.PixelHeight, PixelFormat.Format32bppPArgb);
                BitmapData data = bmp.LockBits(new Rectangle(System.Drawing.Point.Empty, bmp.Size), ImageLockMode.WriteOnly, bmp.PixelFormat);
                s.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
                bmp.UnlockBits(data);

                return bmp;
            }
        }

        //internal static void CreateGif(string filePath, string thuFilePath, FileType ftype, CancellationToken token) {
        //    GifBitmapEncoder gEnc = new();
        //    if (ftype == FileType.FImage) {
        //        Bitmap bitmap = new(filePath);
        //        var src = Imaging.CreateBitmapSourceFromHBitmap(
        //            bitmap.GetHbitmap(),
        //            IntPtr.Zero,
        //            Int32Rect.Empty,
        //            BitmapSizeOptions.FromEmptyOptions());
        //        gEnc.Frames.Add(BitmapFrame.Create(src));
        //        bitmap.Dispose();
        //    }
        //    else if (ftype == FileType.FVideo || ftype == FileType.FGif) {
        //        using var cap = new VideoCapture(filePath);
        //        if (!cap.IsOpened()) {
        //            throw new Exception("An Error occoured");
        //        }

        //        int frameCnt = cap.FrameCount;
        //        int frameLimit = Math.Min(frameCnt, 60);

        //        for (int i = 0; i < frameLimit && !token.IsCancellationRequested; i++) {
        //            cap.Set(VideoCaptureProperties.PosFrames, i);
        //            using Mat frame = new();
        //            cap.Read(frame);
        //            if (frame.Empty()) break;

        //            token.ThrowIfCancellationRequested();

        //            Bitmap bitmap = BitmapConverter.ToBitmap(frame);
        //            frame.Release();

        //            var src = Imaging.CreateBitmapSourceFromHBitmap(
        //            bitmap.GetHbitmap(),
        //            IntPtr.Zero,
        //            Int32Rect.Empty,
        //            BitmapSizeOptions.FromEmptyOptions());
        //            gEnc.Frames.Add(BitmapFrame.Create(src));
        //            bitmap.Dispose();
        //        }

        //        if (token.IsCancellationRequested) {
        //            throw new OperationCanceledException("The video frame reading was canceled.");
        //        }
        //    }

        //    using var ms = new MemoryStream();
        //    gEnc.Save(ms);
        //    var fileBytes = ms.ToArray();
        //    // This is the NETSCAPE2.0 Application Extension.
        //    // 创建循环动画
        //    var _applicationExtension = new byte[] { 33, 255, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0 };
        //    var newBytes = new List<byte>();
        //    newBytes.AddRange(fileBytes.Take(13));
        //    newBytes.AddRange(_applicationExtension);
        //    newBytes.AddRange(fileBytes.Skip(13));
        //    File.WriteAllBytes(thuFilePath, [.. newBytes]);
        //}

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
                case FileType.FImage: {
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

        // Add NETSCAPE2.0 Application Extension for looping.
        private readonly static byte[] _applicationExtension = [33, 255, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0];

        #region web zip
        /// <summary>
        /// 解压 zip 并从 project.json 构建 WpBasicData 元数据。
        /// </summary>
        /// <param name="zipPath">zip 文件路径</param>
        /// <param name="folderPath">目标解压目录（已创建）</param>
        /// <param name="folderName">库文件夹名（用于缩略图命名）</param>
        /// <param name="data">待填充的 WpBasicData 实例</param>
        internal static void BuildBasicDataFromWebZip(
            string zipPath,
            string folderPath,
            string folderName,
            WpBasicData data) {

            // 解压 zip 到目标目录
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, folderPath, overwriteFiles: true);

            // 读 project.json
            string projectJsonPath = Path.Combine(folderPath, "project.json");
            if (!File.Exists(projectJsonPath))
                throw new FileNotFoundException("zip 包内缺少 project.json", projectJsonPath);

            var project = JsonSaver.Load<WpWebProjectData>(
                projectJsonPath,
                WpWebProjectDataContext.Default);

            // HTML 入口文件路径
            string entryRelative = NormalizeRelativePath(project.File);
            string entryAbsolute = Path.Combine(folderPath, entryRelative);
            if (!File.Exists(entryAbsolute))
                throw new FileNotFoundException($"project.json 中指定的 HTML 入口不存在：{project.File}", entryAbsolute);

            data.FilePath = entryAbsolute;

            // 缩略图：复制 preview 到库规范命名
            string thumbnailPath = Path.Combine(folderPath, folderName + Constants.Field.ThumGifSuff);
            string previewRelative = NormalizeRelativePath(project.Preview);
            string previewAbsolute = Path.Combine(folderPath, previewRelative);
            if (File.Exists(previewAbsolute)) {
                File.Copy(previewAbsolute, thumbnailPath, overwrite: true);
            }
            // 找不到预览图则不设置，UI 层显示默认占位图
            data.ThumbnailPath = File.Exists(thumbnailPath) ? thumbnailPath : string.Empty;

            // 文件元数据
            var zipInfo = new FileInfo(zipPath);
            double sizeMb = zipInfo.Length / 1024.0 / 1024.0;
            data.FileSize = sizeMb >= 0.01 ? $"{sizeMb:0.00} MB" : $"{zipInfo.Length / 1024.0:0.00} KB";
            data.FileExtension = ".zip";
            data.Resolution = "Web";
            data.AspectRatio = "-";

            // 元数据字段
            data.Title = string.IsNullOrWhiteSpace(project.Title)
                           ? Path.GetFileNameWithoutExtension(zipPath)
                           : project.Title;
            data.Desc = project.Desc;
            data.Authors = project.Authors;
            data.Tags = project.Tags;
        }

        private static string NormalizeRelativePath(string? raw) {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return raw.Replace('\\', '/').TrimStart('/');
        }
        #endregion
    }
}
