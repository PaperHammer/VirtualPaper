using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MessagePack;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Utils;
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

        internal static async Task SaveAsync(string folderPath, VpCanvas data) {
            try {
                await MessagePackStorage.SaveAsync(GetFilePath(folderPath), data);
            }
            catch (Exception ex) {
                Draft.DraftPanelBridge.GetNotify().ShowExp(ex);
            }
        }

        internal static async Task<VpCanvas> LoadAsync(string folderPath) {
            VpCanvas data = null;
            string filePath = GetFilePath(folderPath);

            try {
                if (!File.Exists(filePath)) {
                    File.Create(filePath).Close();
                    data = VpCanvas.Create();
                    await SaveAsync(folderPath, data); // 确保文件有内容，以保证下次反序列化正确
                }
                else {
                    data = await MessagePackStorage.LoadAsync<VpCanvas>(filePath);
                }
            }
            catch (Exception ex) {
                File.Delete(filePath);
                Draft.DraftPanelBridge.GetNotify().ShowExp(ex);
            }

            return data;
        }

        private static string GetFilePath(string folderPath) {
            return Path.Combine(folderPath, "draft.simd");
        }

        private static readonly float Epsilon = 1e-6f;
    }

    [MessagePackObject(AllowPrivate = true)]
    partial class VpCanvas {
        // 静态工厂方法，用于正常创建对象。避免反序列化时对数据的初始化
        public static VpCanvas Create() {
            var canvas = new VpCanvas();
            canvas.Initialize();

            return canvas;
        }

        private void Initialize() {
            uint dpi = Draft.DraftPanelBridge.GetDpi();
            Size = new SizeF((float)Math.Round(1920.0f / dpi * 96, 1), (float)Math.Round(1080.0f / dpi * 96, 1), dpi);
            LayerList = [new VpLayer()];
        }

        [Key(0)]
        public SizeF Size { get; set; } // 像素
        [Key(1)]
        public List<VpLayer> LayerList { get; set; } // 包含的所有图层
        [Key(2)]
        public int SelectedIndex { get; set; }
        [IgnoreMember]
        public VpLayer SelectedLayer => LayerList[SelectedIndex];
    }

    [MessagePackObject(AllowPrivate = true)]
    class VpLayer {
        public VpLayer() {
            Background = 0xffffffff;
            Opacity = 1.0f;
            Images = [];
            Draws = [];
            IsEnable = true;
            IsVisible = true;
        }

        [Key(0)]
        public uint Background { get; set; }
        [Key(1)]
        public float Opacity { get; set; }
        [Key(2)]
        public bool IsEnable { get; set; }
        [Key(3)]
        public bool IsVisible { get; set; }
        [Key(4)]
        public List<ExternalImg> Images { get; set; } // 包含的所有外部图像
        [Key(5)]
        public List<Draw> Draws { get; set; } // 包含的所有绘制线条
    }

    [MessagePackObject(AllowPrivate = true)]
    class ExternalImg {
        [Key(0)]
        public byte[] ImageData { get; set; } // 图像数据
        [Key(1)]
        public PointF Position { get; set; } // 在Canvas中的位置
        [Key(2)]
        public double Width { get; set; } // 显示时的宽度
        [Key(3)]
        public double Height { get; set; } // 显示时的高度
        [Key(4)]
        public int ZIndex { get; set; } // 层级信息
        [Key(5)]
        public int LayerIndex { get; set; } // 图层信息
    }

    [MessagePackObject(AllowPrivate = true)]
    class Draw {
        [Key(0)]
        public List<PointF> Path { get; set; } = []; // 绘制的路径点
        [Key(1)]
        public uint StrokeColor { get; set; } // 线条颜色
        [Key(2)]
        public double StrokeThickness { get; set; } // 线条宽度
        [Key(3)]
        public int ZIndex { get; set; } // 层级信息
        [Key(4)]
        public int LayerIndex { get; set; } // 图层信息
    }

    struct RectengleF {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    [MessagePackObject(AllowPrivate = true)]
    struct PointF {
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

    [MessagePackObject(AllowPrivate = true)]
    struct SizeF : IEquatable<SizeF> {
        public SizeF(float width, float height, uint dpi) : this() {
            SetSize(width, height, dpi);
        }

        [Key(0)]
        public float Width { get; private set; }
        [Key(1)]
        public float Height { get; private set; }
        [Key(2)]
        public uint Dpi { get; private set; }

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

        public void SetSize(float width, float height, uint dpi) {
            this.Width = CanvasUtil.FormatFloat(width, 1);
            this.Height = CanvasUtil.FormatFloat(height, 1);
            this.Dpi = dpi;
        }
    }
}
