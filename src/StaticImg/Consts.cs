using System;
using System.Text.Json.Serialization;
using MessagePack;
using Windows.Foundation;
using Windows.UI;

namespace Workloads.Creation.StaticImg {
    class Consts {
        public static float MinZoomFactor => 0.2f;
        public static float MaxZoomFactor => 7f;
        public static int LayerThumWidth => 60;
        public static int LayerThumHeight => 38;

        public static double DecimalToPercent(float value) {
            return Math.Round(value * 100, 1);
        }

        internal static double PercentToDeciaml(double value) {
            return Math.Round(value / 100, 1);
        }

        internal static double RoundToNearestFive(double value) {
            return (int)(Math.Round(value, 1) * 10) / 10.0;
        }

        internal static double GetAddStepSize(double curValue) {
            return curValue >= 2.0 - _epsilon ? 0.25 : 0.1;
        }

        internal static double GetSubStepSize(double curValue) {
            return curValue > 2.0 - _epsilon ? 0.25 : 0.1;
        }

        public static bool IsZoomValid(double zoom) {
            float zoomFloat = (float)zoom;
            return zoomFloat >= MinZoomFactor - _epsilon && zoomFloat <= MaxZoomFactor + _epsilon;
        }

        private static readonly float _epsilon = 1e-6f;
    }

    static class UintColor {
        public static uint Transparent => 16777215;
        public static uint White => 4294967295;

        public static uint ToUInt32(this Color color)
            => (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);

        public static Color ToColor(this uint argb)
            => Color.FromArgb(
                (byte)((argb >> 24) & 0xFF), // A
                (byte)((argb >> 16) & 0xFF), // R
                (byte)((argb >> 8) & 0xFF),  // G
                (byte)(argb & 0xFF)          // B
            );

        /// <summary>
        /// 计算带有透明度的颜色和外部传入的透明度百分比混合后的颜色。
        /// </summary>
        /// <param name="color">原始颜色（32 位 ARGB 值）。</param>
        /// <param name="transparencyPercentage">透明度百分比（范围 0 到 1）。</param>
        /// <returns>混合后的颜色（32 位 ARGB 值）。</returns>
        public static uint MixAlpha(uint color, double transparencyPercentage) {
            // 提取原始颜色的 Alpha 通道
            byte originalAlpha = (byte)((color >> 24) & 0xFF);

            // 计算新的 Alpha 值
            byte finalAlpha = (byte)(originalAlpha * transparencyPercentage);

            // 保留原始颜色的 RGB 部分
            uint rgbPart = color & 0x00FFFFFF;

            // 组合新的颜色（AARRGGBB 格式）
            uint finalColor = ((uint)finalAlpha << 24) | rgbPart;

            return finalColor;
        }

        /// <summary>
        /// 根据传入的透明度百分比调整颜色的透明度。
        /// </summary>
        /// <param name="color">原始颜色。</param>
        /// <param name="transparencyPercentage">透明度百分比（范围 0 到 1）。</param>
        /// <returns>混合后的颜色。</returns>
        public static Color MixAlpha(Color color, double transparencyPercentage) {
            byte finalAlpha = (byte)(color.A * transparencyPercentage);

            return Color.FromArgb(finalAlpha, color.R, color.G, color.B);
        }
    }

    [MessagePackObject]
    public struct ArcPoint {
        public ArcPoint(float x, float y) : this() {
            SetPos(x, y);
        }

        public ArcPoint(Point point) : this() {
            SetPos(point);
        }

        [Key(0)]
        public float X { get; private set; }
        [Key(1)]
        public float Y { get; private set; }

        public void SetPos(float x, float y) {
            this.X = (float)Math.Round(x, 1);
            this.Y = (float)Math.Round(y, 1);
        }

        public void SetPos(Point point) {
            SetPos((float)point.X, (float)point.Y);
        }

        public static ArcPoint FormatPoint(Point point, int digit) {
            return new ArcPoint((float)Math.Round(point.X, digit), (float)Math.Round(point.Y, digit));
        }

        public static ArcPoint ToArcPoint(Point point) {
            return new ArcPoint((float)point.X, (float)point.Y);
        }

        public static Point ToPoint(ArcPoint arcPoint) {
            return new Point(arcPoint.X, arcPoint.Y);
        }
    }

    readonly struct ArcSize : IEquatable<ArcSize> {
        [JsonConstructor]
        [Obsolete("This constructor is intended for JSON deserialization only. Use the another method instead.")]
        internal ArcSize(double width, double height, uint dpi) {
            this.Dpi = dpi;
            this.Width = width;
            this.Height = height;
            this.Ratio = (float)(1.0f * width / height);
        }

        public ArcSize(
            double width,
            double height,
            uint dpi,
            RebuildMode rebuild) {
            this.Dpi = dpi;
            (this.Width, this.Height) = rebuild switch {
                RebuildMode.RotateLeft or RebuildMode.RotateRight => (height, width),
                _ => (width, height)
            };
            this.Rebuild = rebuild;
            this.Ratio = (float)(1.0f * width / height);
        }

        public double Width { get; }
        public double Height { get; }
        public uint Dpi { get; }
        [JsonIgnore]
        public float Ratio { get; } // 宽高比
        [JsonIgnore]
        public RebuildMode Rebuild { get; }
        [JsonIgnore]
        public static uint HardwareDpi => MainPage.Instance.Bridge.GetHardwareDpi();

        // readonly 关键字在此处意味着这个方法不会修改任何实例的状态（即它不会改变对象的任何字段）。
        // 这有助于编译器优化，并明确地传达了该方法是纯粹基于现有数据进行计算而不改变对象状态的事实。
        public readonly bool Equals(ArcSize other) {
            return Width == other.Width && Height == other.Height && Dpi == other.Dpi;
        }

        public override readonly bool Equals(object obj)
            => obj is ArcSize objS && Equals(objS);

        public override readonly int GetHashCode()
            => HashCode.Combine(Width, Height, Dpi);
        public static bool operator ==(ArcSize left, ArcSize right) {
            return left.Equals(right);
        }

        public static bool operator !=(ArcSize left, ArcSize right) {
            return !(left == right);
        }

        public static double Area(Size size) => size.Width * size.Height;

        internal Size GetSize() {
            return new Size(Width, Height);
        }
    }

    // TODO
    enum PaintBrushType {
        CommonBrush, // 画笔
        //WritingBrush, // 毛笔
        //Pen, // 书写笔(钢笔 ???)
        //Airbrush, // 喷枪
        //OilBrush, // 油画笔
        //Crayon, // 蜡笔
        //MarkerPen, // 记号笔
        //OrdinaryPencil, // 普通铅笔
        //WatercolorBrush, // 水彩画笔
    }

    // TODO
    enum ToolType {
        None,
        Eraser, // 橡皮擦
        PaintBrush, // 画笔
        //Text, // 文本
        //MainSource, // 图片
        //Shape, // 图形
        Fill, // 填充
        //Lasso, // 套索工具
        Crop, // 裁剪
        Selection,
        CanvasSet,
    }

    // rotate per 90 degree
    enum RebuildMode {
        None, ResizeExpand, ResizeScale, RotateLeft, RotateRight, FlipHorizontal, FlipVertical,
    }
}
