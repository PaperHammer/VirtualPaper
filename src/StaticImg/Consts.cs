using System;
using System.Collections.Generic;
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

    abstract record BaseElement {
        [Key(0)]
        public int ZIndex { get; set; } // 层级
        [Key(1)]
        public long ZTime { get; set; } // 写入的时间
        [IgnoreMember]
        public virtual BaseElementType Type { get; set; }
        [IgnoreMember]
        public bool IsSaved { get; set; }
    }

    [MessagePackObject(AllowPrivate = true)]
    record STAImage : BaseElement {
        [Key(2)]
        public override BaseElementType Type => BaseElementType.Image;
        [Key(3)]
        public byte[] Data { get; set; } // 图像数据
        [Key(4)]
        public PointF Position { get; set; } // 在Canvas中的位置
        [Key(5)]
        public double Width { get; set; } // 显示时的宽度
        [Key(6)]
        public double Height { get; set; } // 显示时的高度               
    }

    [MessagePackObject(AllowPrivate = true)]
    record STADraw : BaseElement {
        [Key(2)]
        public override BaseElementType Type => BaseElementType.Draw;
        [Key(3)]
        public List<PointF> Points { get; set; } = []; // 绘制的路径点
        [Key(4)]
        public uint StrokeColor { get; set; } // 线条颜色
        [Key(5)]
        public double StrokeThickness { get; set; } // 线条宽度
    }

    public struct RectengleF {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    [MessagePackObject(AllowPrivate = true)]
    public struct PointF {
        public PointF(float x, float y) : this() {
            SetPos(x, y);
        }

        public PointF(Point point) : this() {
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
    }

    public struct SizeF : IEquatable<SizeF> {
        [JsonConstructor]
        public SizeF(int width, int height, uint dpi) {
            this.Width = width;
            this.Height = height;
            this.Dpi = dpi;
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public uint Dpi { get; private set; }
        public readonly uint HardwareDpi => MainPage.Instance.Bridge.GetDpi();

        // readonly 关键字在此处意味着这个方法不会修改任何实例的状态（即它不会改变对象的任何字段）。
        // 这有助于编译器优化，并明确地传达了该方法是纯粹基于现有数据进行计算而不改变对象状态的事实。
        public readonly bool Equals(SizeF other) {
            return Width == other.Width && Height == other.Height && Dpi == other.Dpi;
        }

        public override readonly bool Equals(object obj)
            => obj is SizeF objS && Equals(objS);

        public override readonly int GetHashCode()
            => HashCode.Combine(Width, Height, Dpi);
    }

    enum BaseElementType {
        Image,
        Draw
    }

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

    enum ToolType {
        None,
        Eraser, // 橡皮擦
        PaintBrush, // 画笔
        Text, // 文本
        Image, // 图片
        Shape, // 图形
        ColorPicker, // 取色器
        Fill, // 填充
        //Lasso, // 套索工具
        Crop, // 裁剪
    }
}
