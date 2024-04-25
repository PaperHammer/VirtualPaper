using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.PInvoke;
using Application = System.Windows.Application;
using Matrix = System.Windows.Media.Matrix;
using Size = System.Windows.Size;

namespace VirtualPaper.Utils
{
    public class WpfUtil
    {
        /// <summary>
        /// makes program window handle child of window ui framework element.
        /// </summary>
        /// <param name="window"></param>
        /// <param name="pgmHandle"></param>
        /// <param name="element"></param>
        public static void SetProgramToFramework(Window window, IntPtr pgmHandle, FrameworkElement element)
        {
            IntPtr previewHwnd = new WindowInteropHelper(window).Handle;
            Native.RECT prct = new Native.RECT();
            var reviewPanel = GetAbsolutePlacement(element, true);

            if (!Native.SetWindowPos(pgmHandle, 1, (int)reviewPanel.Left, (int)reviewPanel.Top, (int)reviewPanel.Width, (int)reviewPanel.Height, 0 | 0x0010))
            {
                throw new Exception(LogUtil.GetWin32Error("Failed to set parent (1)"));
            }

            //ScreentoClient is no longer used, this supports windows mirrored mode also, calculate new relative position of window w.r.t parent.
            _ = Native.MapWindowPoints(pgmHandle, previewHwnd, ref prct, 2);
            WindowUtil.SetParentSafe(pgmHandle, previewHwnd);

            //Position the wp window relative to the new parent window(workerw).
            if (!Native.SetWindowPos(pgmHandle, 1, prct.Left, prct.Top, (int)reviewPanel.Width, (int)reviewPanel.Height, 0 | 0x0010))
            {
                throw new Exception(LogUtil.GetWin32Error("Failed to set parent (2)"));
            }
        }

        //https://stackoverflow.com/questions/386731/get-absolute-position-of-element-within-the-window-in-wpf
        /// <summary>
        /// Get UI Framework element position.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="relativeToScreen">false: w.r.t application</param>
        /// <returns></returns>
        public static Rect GetAbsolutePlacement(FrameworkElement element, bool relativeToScreen = false)
        {
            var absolutePos = element.PointToScreen(new System.Windows.Point(0, 0));
            if (relativeToScreen)
            {
                //taking display dpi into account..
                var pixelSize = GetElementPixelSize(element);
                return new Rect(absolutePos.X, absolutePos.Y, pixelSize.Width, pixelSize.Height);
            }
            var posMW = Application.Current.MainWindow.PointToScreen(new System.Windows.Point(0, 0));
            absolutePos = new System.Windows.Point(absolutePos.X - posMW.X, absolutePos.Y - posMW.Y);
            return new Rect(absolutePos.X, absolutePos.Y, element.ActualWidth, element.ActualHeight);
        }

        //https://stackoverflow.com/questions/3286175/how-do-i-convert-a-wpf-size-to-physical-pixels
        /// <summary>
        /// Retrieves pixel size of UI element, taking display scaling into account.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static Size GetElementPixelSize(UIElement element)
        {
            Matrix transformToDevice;
            var source = PresentationSource.FromVisual(element);
            if (source != null)
                transformToDevice = source.CompositionTarget.TransformToDevice;
            else
                using (var source1 = new HwndSource(new HwndSourceParameters()))
                    transformToDevice = source1.CompositionTarget.TransformToDevice;

            if (element.DesiredSize == new Size())
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            return (Size)transformToDevice.Transform((Vector)element.DesiredSize);
        }

        private const int LWA_ALPHA = 0x2;
        private const int LWA_COLORKEY = 0x1;

        /// <summary>
        /// Set window alpha.
        /// </summary>
        /// <param name="Handle"></param>
        public static void SetWindowTransparency(IntPtr Handle)
        {
            var styleCurrentWindowExtended = Native.GetWindowLongPtr(Handle, (-20));
            var styleNewWindowExtended =
                styleCurrentWindowExtended.ToInt64() ^
                Native.WindowStyles.WS_EX_LAYERED;

            Native.SetWindowLongPtr(new HandleRef(null, Handle), (int)Native.GWL.GWL_EXSTYLE, (IntPtr)styleNewWindowExtended);
            Native.SetLayeredWindowAttributes(Handle, 0, 128, LWA_ALPHA);
        }

        //public static void CreateGifByImages(string thumbnailPath, List<Bitmap> images, CancellationToken token)
        //{
        //    GifBitmapEncoder gEnc = new();            
        //    foreach (Bitmap bmpImage in images)
        //    {
        //        token.ThrowIfCancellationRequested();

        //        //using Bitmap resizedBmp = ResizeBitmap(bmpImage, 300, 200);
        //        //bmpImage.Dispose();

        //        var src = Imaging.CreateBitmapSourceFromHBitmap(
        //            bmpImage.GetHbitmap(),
        //            IntPtr.Zero,
        //            Int32Rect.Empty,
        //            BitmapSizeOptions.FromEmptyOptions());
        //        gEnc.Frames.Add(BitmapFrame.Create(src));
        //        bmpImage.Dispose();
        //    }

        //    token.ThrowIfCancellationRequested(); // 在保存前再次检查

        //    //using FileStream fs = new(thumbnailPath, FileMode.Create);
        //    //gEnc.Save(fs);

        //    using (var ms = new MemoryStream())
        //    {
        //        gEnc.Save(ms);
        //        var fileBytes = ms.ToArray();
        //        // This is the NETSCAPE2.0 Application Extension.
        //        // 创建循环动画
        //        var applicationExtension = new byte[] { 33, 255, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0 };
        //        var newBytes = new List<byte>();
        //        newBytes.AddRange(fileBytes.Take(13));
        //        newBytes.AddRange(applicationExtension);
        //        newBytes.AddRange(fileBytes.Skip(13));
        //        File.WriteAllBytes(thumbnailPath, newBytes.ToArray());
        //    }
        //    images.Clear();
        //}

        //private static Bitmap ResizeBitmap(Bitmap source, int width, int height)
        //{
        //    var destRect = new Rectangle(0, 0, width, height);
        //    var destImage = new Bitmap(width, height);

        //    destImage.SetResolution(source.HorizontalResolution, source.VerticalResolution);

        //    using (var graphics = Graphics.FromImage(destImage))
        //    {
        //        graphics.CompositingMode = CompositingMode.SourceCopy;
        //        graphics.CompositingQuality = CompositingQuality.HighQuality;
        //        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        //        graphics.SmoothingMode = SmoothingMode.HighQuality;
        //        graphics.DrawImage(source, destRect, 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
        //    }

        //    return destImage;
        //}
    }
}
