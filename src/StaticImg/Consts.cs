using System;
using System.IO;
using System.Text.Json.Serialization;
using VirtualPaper.UIComponent;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Core.Utils;

namespace Workloads.Creation.StaticImg {
    class Consts {
        public static int MAX_CANVAS_SIZE_WITH_DPI => (int)(1.0F * _maxCanvasdge / WindowConsts.ArcWindowInstance.Content.XamlRoot.RasterizationScale * 96);
        public static float MinZoomFactor => 0.2f;
        public static float MaxZoomFactor => 8f;
        public static int LayerThumWidth => 60;
        public static int LayerThumHeight => 38;

        public static void SetMaxCanvasEdge(int maxCanvasdge) {
            _maxCanvasdge = maxCanvasdge;
        }

        public static double DecimalToPercent(double value) {
            return DecimalToPercent(value, 1);
        }
        
        public static double DecimalToPercent(float value) {
            return DecimalToPercent(value, 1);
        }

        public static double DecimalToPercent(double value, int digits) {
            return Math.Round(value * 100, digits);
        }

        // 保留 3 位小数，避免从 Slider 传入的数值被四舍五入导致精度丢失
        internal static double PercentToDeciaml(float value, int digits = 3) {
            return Math.Round(value / 100, digits);
        }

        internal static double RoundToNearestFive(double value, int digits = 1) {
            return (Math.Round(value, digits) * 10) / 10.0;
        }

        internal static double GetAddStepSize(double curValue) {
            return curValue >= 2.0 - _epsilon ? 0.25 : 0.1;
        }

        internal static double GetSubStepSize(double curValue) {
            return curValue > 2.0 - _epsilon ? 0.25 : 0.1;
        }

        public static bool IsZoomValid(double zoom) {
            return zoom >= MinZoomFactor - _epsilon && zoom <= MaxZoomFactor + _epsilon;
        }

        public static bool IsDoubleValueEqual(double a, double b) {
            return Math.Abs(a - b) < Math.Pow(10, -5);
        }

        public static bool IsPointerOverTaregt(Point? position, ArcSize range) {
            return position != null && position.Value.X >= 0 && position.Value.X < range.Width &&
                   position.Value.Y >= 0 && position.Value.Y < range.Height;
        }

        public static bool IsRectIntersect(Rect rect1, Rect rect2) {
            return rect1.X < rect2.X + rect2.Width &&
                   rect1.X + rect1.Width > rect2.X &&
                   rect1.Y < rect2.Y + rect2.Height &&
                   rect1.Y + rect1.Height > rect2.Y;
        }

        public static bool TryGetIntersect(Rect rect1, Rect rect2, out Rect intersect) {
            intersect = Rect.Empty;
            if (!IsRectIntersect(rect1, rect2)) {
                return false;
            }

            double x = Math.Max(rect1.X, rect2.X);
            double y = Math.Max(rect1.Y, rect2.Y);
            double width = Math.Min(rect1.X + rect1.Width, rect2.X + rect2.Width) - x;
            double height = Math.Min(rect1.Y + rect1.Height, rect2.Y + rect2.Height) - y;
            intersect = new Rect(x, y, width, height);
            
            return true;
        }

        internal static void InitData(InkProjectSession session) {
            _maxCanvasdge = session.SharedDevice.MaximumBitmapSizeInPixels;
        }

        private static readonly float _epsilon = 1e-6f;
        private static int _maxCanvasdge;
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

    public readonly struct ArcSize : IEquatable<ArcSize> {
        [JsonConstructor]
        [Obsolete("This constructor is intended for JSON deserialization only. Use the another method instead.")]
        internal ArcSize(float width, float height, uint dpi) {
            this.Dpi = dpi;
            this.Width = width;
            this.Height = height;
            this.Ratio = (float)(1.0f * width / height);
        }

        public ArcSize(
            float width,
            float height,
            uint dpi,
            RebuildMode rebuild) {
            this.Dpi = dpi;
            this.Width = width;
            this.Height = height;
            this.Rebuild = rebuild;
            this.Ratio = width / height;
        }

        public float Width { get; }
        public float Height { get; }
        public uint Dpi { get; }
        [JsonIgnore]
        public float Ratio { get; } // 宽高比
        [JsonIgnore]
        public RebuildMode Rebuild { get; }
        [JsonIgnore]
        public Rect Bound => new(0, 0, Width, Height);

        // readonly 关键字在此处意味着这个方法不会修改任何实例的状态（即它不会改变对象的任何字段）。
        // 这有助于编译器优化，并明确地传达了该方法是纯粹基于现有数据进行计算而不改变对象状态的事实。
        public readonly bool Equals(ArcSize other)
            => (Width, Height, Dpi, Ratio, Rebuild) == (other.Width, other.Height, other.Dpi, other.Ratio, other.Rebuild);

        public override readonly bool Equals(object? obj) => obj is ArcSize objS && Equals(objS);

        public override readonly int GetHashCode() => HashCode.Combine(Width, Height, Dpi);

        public static bool operator ==(ArcSize left, ArcSize right) => left.Equals(right);

        public static bool operator !=(ArcSize left, ArcSize right) => !(left == right);

        public static double Area(Size size) => size.Width * size.Height;

        public Size ToSize() => new(Width, Height);
        public Rect ToRect() => new(0, 0, Width, Height);
        public bool IsRotate() => Rebuild == RebuildMode.RotateLeft || Rebuild == RebuildMode.RotateRight;
        public bool IsFlip() => Rebuild == RebuildMode.FlipVertical || Rebuild == RebuildMode.FlipHorizontal;
        public bool IsResizeExpand() => Rebuild == RebuildMode.ResizeExpand;
        public bool IsResizeScale() => Rebuild == RebuildMode.ResizeScale;
    }

    // TODO
    public enum ToolType {
        None,
        Eraser, // 橡皮擦
        PaintBrush, // 画笔
        //Text, // 文本
        //MainSource, // 图片
        //Style, // 图形
        Fill, // 填充
        //Lasso, // 套索工具
        Crop, // 裁剪
        Selection,
        CanvasSet,
    }

    // rotate per 90 degree
    public enum RebuildMode {
        None, ResizeExpand, ResizeScale, RotateLeft, RotateRight, FlipHorizontal, FlipVertical,
    }

    public enum CanvasOperation {
        RotateLeft,
        RotateRight,
        FlipHorizontally,
        FlipVertically,
    }

    public struct LayerState {
        public bool IsVisible;
        public bool IsDeleted;
        public int ZIndex;

        public void Serialize(BinaryWriter writer) {
            writer.Write(IsVisible);
            writer.Write(ZIndex);
        }

        public static LayerState Deserialize(BinaryReader reader) {
            return new LayerState {
                IsVisible = reader.ReadBoolean(),
                ZIndex = reader.ReadInt32()
            };
        }
    }
}
