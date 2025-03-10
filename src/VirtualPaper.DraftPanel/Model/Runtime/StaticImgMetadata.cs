using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MessagePack;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.DraftPanel.Model.Runtime {
    [MessagePackObject(AllowPrivate = true)]
    class StaticImgMetadata {
        public StaticImgMetadata() {
            CanvasConfig = new();
            Images = [];
            Draws = [];
        }

        [Key(0)]
        public VpCanvas CanvasConfig { get; set; }

        [Key(1)]
        public List<ExternalImg> Images { get; set; } // 包含的所有外部图像

        [Key(2)]
        public List<Draw> Draws { get; set; } // 包含的所有绘制线条

        public async Task SaveAsync(string folderPath) {
            try {
                await MessagePackStorage.SaveAsync(Path.Combine(folderPath, "draft.simd"), this);
            }
            catch (System.Exception ex) {
                Draft.DraftPanelBridge.GetNotify().ShowExp(ex);
            }
        }

        public async Task LoadAsync(string folderPath) {
            try {
                var data = await MessagePackStorage.LoadAsync<StaticImgMetadata>(Path.Combine(folderPath, "draft.simd"));
                CanvasConfig = data.CanvasConfig;
                Images = data.Images;
                Draws = data.Draws;
            }
            catch (System.Exception ex) {
                Draft.DraftPanelBridge.GetNotify().ShowExp(ex);
            }
        }
    }

    [MessagePackObject(AllowPrivate = true)]
    internal partial class VpCanvas : ObservableObject {
        public VpCanvas() {
            float scale = (float)Draft.DraftPanelBridge.GetScale();
            Width = Width / scale * (DPI / 96);
            Height = Height / scale * (DPI / 96);
        }

        [IgnoreMember]
        float _width = 1920; // 像素
        [Key(0)]
        public float Width {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        [IgnoreMember]
        float _height = 1080; // 像素
        [Key(1)]
        public float Height {
            get => _height;
            set { _height = value; OnPropertyChanged(); }
        }

        [IgnoreMember]
        uint _background = 0xffffffff;
        [Key(2)]
        public uint Background {
            get => _background;
            set { _background = value; OnPropertyChanged(); }
        }

        [IgnoreMember]
        private int _dpi = 96;
        [Key(3)]
        public int DPI {
            get { return _dpi; }
            set { _dpi = value; OnPropertyChanged(); }
        }
    }

    [MessagePackObject(AllowPrivate = true)]
    class ExternalImg {
        [Key(0)]
        public byte[] ImageData { get; set; } // 图像数据
        [Key(1)]
        public double[] Position { get; set; } // 在Canvas中的位置
        [Key(2)]
        public double Width { get; set; } // 显示时的宽度
        [Key(3)]
        public double Height { get; set; } // 显示时的高度
        [Key(4)]
        public int ZIndex { get; set; } // 层级信息
    }

    [MessagePackObject(AllowPrivate = true)]
    class Draw {
        [Key(0)]
        public List<double[]> Path { get; set; } = []; // 绘制的路径点
        [Key(1)]
        public uint StrokeColor { get; set; } // 线条颜色
        [Key(2)]
        public double StrokeThickness { get; set; } // 线条宽度
        [Key(3)]
        public int ZIndex { get; set; } // 层级信息
    }
}
