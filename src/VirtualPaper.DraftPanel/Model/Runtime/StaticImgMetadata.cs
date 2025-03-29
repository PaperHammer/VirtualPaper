using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MessagePack;
using Windows.Foundation;

namespace VirtualPaper.DraftPanel.Model.Runtime {
    class StaticImgMetadata {
        public static float MinZoomFactor => 0.2f;
        public static float MaxZoomFactor => 7f;

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
            return curValue >= 2.0 - Epsilon ? 0.25 : 0.1;
        }

        internal static double GetSubStepSize(double curValue) {
            return curValue > 2.0 - Epsilon ? 0.25 : 0.1;
        }

        public static bool IsZoomValid(double zoom) {
            float zoomFloat = (float)zoom;
            return zoomFloat >= MinZoomFactor - Epsilon && zoomFloat <= MaxZoomFactor + Epsilon;
        }

        private static readonly float Epsilon = 1e-6f;
    }

    abstract record StaticImgElement {
        [Key(0)]
        public int ZIndex { get; set; } // 层级
        [Key(1)]
        public long ZTime { get; set; } // 写入的时间
        [IgnoreMember]
        public virtual StaticImgElementType Type { get; set; }
        [IgnoreMember]
        public bool IsSaved { get; set; }
    }

    [MessagePackObject(AllowPrivate = true)]
    record STAImage : StaticImgElement {
        [Key(2)]
        public override StaticImgElementType Type => StaticImgElementType.Image;
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
    record STADraw : StaticImgElement {
        [Key(2)]
        public override StaticImgElementType Type => StaticImgElementType.Draw;
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
        public SizeF(float width, float height, uint dpi) {
            this.Width = (float)Math.Round(width, 1);
            this.Height = (float)Math.Round(height, 1);
            this.Dpi = dpi;
        }

        public float Width { get; private set; }
        public float Height { get; private set; }
        public uint Dpi { get; private set; }
        public readonly uint HardwareDpi => Draft.Instance.GetDpi();

        // readonly 关键字在此处意味着这个方法不会修改任何实例的状态（即它不会改变对象的任何字段）。
        // 这有助于编译器优化，并明确地传达了该方法是纯粹基于现有数据进行计算而不改变对象状态的事实。
        public readonly bool Equals(SizeF other) {
            return Width == other.Width && Height == other.Height && Dpi == other.Dpi;
        }

        public override readonly bool Equals(object obj)
            => obj is SizeF objS && Equals(objS);

        public override readonly int GetHashCode()
            => HashCode.Combine(Width, Height, Dpi);

        public static bool operator ==(SizeF left, SizeF right)
            => left.Equals(right);

        public static bool operator !=(SizeF left, SizeF right)
            => !left.Equals(right);
    }

    enum StaticImgElementType {
        Image,
        Draw
    }
}
