using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;
using Workloads.Creation.StaticImg.Utils;
using Workloads.Creation.StaticImg.Views;
using static VirtualPaper.Common.Utils.Archive.ZipUtil;

namespace Workloads.Creation.StaticImg.Models {
    [JsonSerializable(typeof(CanvasLayerData))]
    internal partial class CanvasLayerDataContext : JsonSerializerContext { }

    internal partial class CanvasLayerData : ObservableObject, IEquatable<CanvasLayerData> {
        public event EventHandler OnDataLoaded;
        public event EventHandler<PathEventArgs> OnDrawsChanging;
        public event EventHandler OnDrawsChanged;

        [JsonIgnore]
        public TaskCompletionSource<bool> RenderCompleted => _renderCompleted;

        private string _name = string.Empty;
        public string Name {
            get => _name;
            set { if (_name == value) return; _name = value; OnPropertyChanged(); }
        }

        public long Tag { get; }
        public bool IsRootBackground { get; }

        //uint _background = UintColor.Transparent;
        //public uint Background {
        //    get => _background;
        //    set { if (_background == value) return; _background = value; OnPropertyChanged(); }
        //}

        float _opacity = 1f;
        public float Opacity {
            get => _opacity;
            set { if (_opacity == value) return; _opacity = value; OnPropertyChanged(); }
        }

        bool _isEnable = true;
        public bool IsEnable {
            get => _isEnable;
            set {
                if (_isEnable == value) return;
                _isEnable = value;
                if (value)
                    MainPage.Instance.Bridge.GetNotify().CloseAndRemoveMsg(nameof(Constants.I18n.Draft_SI_LayerLocked));
                OnPropertyChanged();
            }
        }

        private int _zIndex;
        public int ZIndex {
            get { return _zIndex; }
            set { _zIndex = value; OnPropertyChanged(); }
        }

        ImageSource _layerThum;
        [JsonIgnore]
        public ImageSource LayerThum {
            get { return _layerThum; }
            set { _layerThum = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public WriteableBitmap BitmapData { get; set; }

        //[Key(1)]
        //[JsonIgnore]
        //public ObservableCollection<STAImage> Images { get; private set; } = []; // 包含的所有外部图像
        //[Key(2)]
        //[JsonIgnore]
        //public ObservableCollection<STADraw> Draws { get; private set; } = []; // 包含的所有绘制线条

        [JsonConstructor]
        [Obsolete("This constructor is intended for JSON deserialization only. Use the another method instead.")]
        public CanvasLayerData(long tag, bool isRootBackground) {
            Tag = tag;
            IsRootBackground = isRootBackground;
        }

        public CanvasLayerData(string filePath, int width, int height, bool isBackground = false) {
            _width = width;
            _height = height;
            IsRootBackground = isBackground;
            Tag = IdentifyUtil.GenerateIdShort();
            SetFilePath(filePath);
            InitBitmap();
        }

        private async void InitBitmap() {
            BitmapData = new(_width, _height);
            if (IsRootBackground) {
                await WriteableBitmapExtension.FillWithWhiteAsync(BitmapData);
            }
        }

        public async Task<CanvasLayerData> CopyAsync() {
            CanvasLayerData newLayerData = new(_filePath, _width, _height) {
                //Background = this.Background,
                BitmapData = await this.BitmapData.DeepCopyAsync(),
                Opacity = this.Opacity,
                IsEnable = this.IsEnable,
                //Images = [.. this.Images],
                //Draws = [.. this.Draws],
            };

            return newLayerData;
        }

        public async Task SaveAsync() {
            await _saveQueueLock.WaitAsync();
            try {
                //await _drawSaver.SaveToBufferAsync(Draws, filePathForDarws);
                //await _drawSaver.SaveManuallyAsync();
                //await _imageSaver.SaveToBufferAsync(Images, filePathForImages);
                //await _imageSaver.SaveManuallyAsync();

                //await MessagePackSaver.SaveAsync(_filePathForDarws, Draws);
                //await MessagePackSaver.SaveAsync(_filePathForImages, Images);

                await WriteableBitmapExtension.WriteableBitmapToFileAsync(BitmapData, _filePathForBitmap);
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Project_STI_LayerSaveFailed), InfoBarType.Error, Name, Tag.ToString(), false);
            }
            finally {
                _saveQueueLock.Release();
            }
        }

        public async Task LoadAsync() {
            try {
                //this.Draws = await MessagePackSaver.LoadAsync<ObservableCollection<STADraw>>(_filePathForDarws) ?? [];
                //this.Images = await MessagePackSaver.LoadAsync<ObservableCollection<STAImage>>(_filePathForImages) ?? [];

                BitmapData = await WriteableBitmapExtension.ReadWriteableBitmapFromFileAsync(_filePathForBitmap);

                OnDataLoaded?.Invoke(this, EventArgs.Empty);

                await RenderCompleted.Task;
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Project_STI_LayerLoadFailed), InfoBarType.Error, Name, Tag.ToString(), false);
            }
        }

        //internal void AddDraw(Path currentPath, STADraw currentDraw) {
        //    //Draws.Add(currentDraw);
        //    //OnDrawsChanging?.Invoke(this, new(currentPath, OperationType.Add));
        //}

        //internal void RemoveDraw(Path currentPath, STADraw currentDraw) {
        //    //Draws.Remove(currentDraw);
        //    //OnDrawsChanging?.Invoke(this, new(currentPath, OperationType.Remove));
        //}

        //internal void DrawsChanged() {
        //    //OnDrawsChanged?.Invoke(this, EventArgs.Empty);
        //}

        public void SetFilePath(string filePath) {
            _filePath = filePath;
            string newFolderPath = System.IO.Path.GetDirectoryName(filePath);
            _filePathForBitmap = System.IO.Path.Combine(newFolderPath, Tag + ".bitmap");
            //_filePathForDarws = System.IO.Path.Combine(newFolderPath, Tag + ".draws");
            //_filePathForImages = System.IO.Path.Combine(newFolderPath, Tag + ".images");
        }

        internal async Task DeletAsync() {
            await FileUtil.TryDeleteFileAsync(_filePathForBitmap, 0, 0);
            //await FileUtil.TryDeleteFileAsync(_filePathForDarws, 0, 0);
            //await FileUtil.TryDeleteFileAsync(_filePathForImages, 0, 0);
        }

        //internal async Task DrawLineAsync(int x0, int y0, int x1, int y1, Color color) {
        //    int dx = Math.Abs(x1 - x0);
        //    int dy = Math.Abs(y1 - y0);
        //    int sx = x0 < x1 ? 1 : -1;
        //    int sy = y0 < y1 ? 1 : -1;
        //    int err = dx - dy;

        //    while (true) {
        //        //await RenderWithAntiAliasing(x0, y0, color, 1); // 绘制当前点
        //        await RenderAsync(x0, y0, color);

        //        if (x0 == x1 && y0 == y1) break;

        //        int e2 = 2 * err;
        //        if (e2 > -dy) {
        //            err -= dy;
        //            x0 += sx;
        //        }
        //        if (e2 < dx) {
        //            err += dx;
        //            y0 += sy;
        //        }
        //    }
        //}

        //internal async Task RenderAsync(int x, int y, Color color) {
        //    // 获取像素缓冲区
        //    using (var stream = BitmapData.PixelBuffer.AsStream()) {
        //        // 计算像素的偏移量（每个像素占用 4 字节：BGRA）
        //        int bytesPerPixel = 4;
        //        int stride = BitmapData.PixelWidth * bytesPerPixel; // 每行的字节数
        //        int offset = y * stride + x * bytesPerPixel;

        //        if (offset < 0 || offset >= stream.Length) {
        //            return;
        //        }

        //        stream.Position = offset;

        //        // 写入颜色值（BGRA 格式）
        //        byte[] pixelBytes = new byte[bytesPerPixel];
        //        pixelBytes[0] = color.B;
        //        pixelBytes[1] = color.G;
        //        pixelBytes[2] = color.R;
        //        pixelBytes[3] = color.A;

        //        await stream.WriteAsync(pixelBytes);
        //    }

        //    // 通知 WriteableBitmap 数据已更改
        //    BitmapData.Invalidate();
        //}

        //public async Task RenderAsync(int x, int y, Color color, double brushSize) {
        //    double radius = brushSize / 2;

        //    for (int dy = -(int)Math.Floor(radius); dy <= (int)Math.Ceiling(radius); dy++) {
        //        for (int dx = -(int)Math.Floor(radius); dx <= (int)Math.Ceiling(radius); dx++) {
        //            if (dx * dx + dy * dy <= radius * radius) {
        //                // 判断是否在圆形范围内
        //                int px = x + dx;
        //                int py = y + dy;

        //                if (px >= 0 && px < BitmapData.PixelWidth &&
        //                    py >= 0 && py < BitmapData.PixelHeight) {
        //                    await SetPixelAsync(px, py, color);
        //                }
        //            }
        //        }
        //    }

        //    BitmapData.Invalidate();
        //}

        public async Task RenderAsync(int x, int y, Color color, double brushSize) {
            double radius = brushSize / 2;
            int radiusInt = (int)Math.Ceiling(radius);
            int radiusSquared = (int)(radius * radius);

            // 边界裁剪
            int startX = Math.Max(0, x - radiusInt);
            int endX = Math.Min(BitmapData.PixelWidth - 1, x + radiusInt);
            int startY = Math.Max(0, y - radiusInt);
            int endY = Math.Min(BitmapData.PixelHeight - 1, y + radiusInt);

            if (brushSize > 20) {
                // 当 brushSize > 20 时启用并行化
                await RenderParallelAsync(startY, endY, startX, endX, x, y, color, radiusSquared);
            }
            else {
                // 普通渲染（利用对称性）
                RenderSymmetric(x, y, color, radiusInt, radiusSquared);
            }

            BitmapData.Invalidate();
        }

        private void RenderSymmetric(int x, int y, Color color, int radiusInt, int radiusSquared) {
            for (int dy = 0; dy <= radiusInt; dy++) {
                for (int dx = 0; dx <= radiusInt; dx++) {
                    if (dx * dx + dy * dy <= radiusSquared) {
                        // 映射到8个象限
                        _ = SetPixelAsync(x + dx, y + dy, color); // 第一象限
                        _ = SetPixelAsync(x - dx, y + dy, color); // 第二象限
                        _ = SetPixelAsync(x + dx, y - dy, color); // 第四象限
                        _ = SetPixelAsync(x - dx, y - dy, color); // 第三象限

                        // 如果不在轴上，还要考虑水平和垂直对称
                        if (dx != 0 && dy != 0) {
                            _ = SetPixelAsync(x + dy, y + dx, color); // 对角线对称
                            _ = SetPixelAsync(x - dy, y + dx, color);
                            _ = SetPixelAsync(x + dy, y - dx, color);
                            _ = SetPixelAsync(x - dy, y - dx, color);
                        }
                    }
                }
            }
        }

        private async Task RenderParallelAsync(int startY, int endY, int startX, int endX, int x, int y, Color color, int radiusSquared) {
            // 使用并行化处理
            await Task.Run(() => {
                Parallel.For(startY, endY + 1, py => {
                    for (int px = startX; px <= endX; px++) {
                        int dx = px - x;
                        int dy = py - y;

                        if (dx * dx + dy * dy <= radiusSquared) {
                            _ = SetPixelAsync(px, py, color);
                        }
                    }
                });
            });
        }

        private async Task SetPixelAsync(int x, int y, Color color) {
            using (var stream = BitmapData.PixelBuffer.AsStream()) {
                int bytesPerPixel = 4;
                int stride = BitmapData.PixelWidth * bytesPerPixel;
                int offset = y * stride + x * bytesPerPixel;

                if (offset < 0 || offset >= stream.Length) {
                    return;
                }

                stream.Position = offset;

                byte[] pixelBytes = new byte[bytesPerPixel];
                pixelBytes[0] = color.B;
                pixelBytes[1] = color.G;
                pixelBytes[2] = color.R;
                pixelBytes[3] = color.A;

                await stream.WriteAsync(pixelBytes);
            }
        }

        //unsafe void FastPixelUpdate(WriteableBitmap bitmap, int x, int y, Color color) {
        //    using (var buffer = bitmap.PixelBuffer.AsStream())
        //    using (var reference = bitmap.PixelBuffer.CreateReference()) {
        //        // 获取原生内存指针
        //        byte* pixelData;
        //        uint capacity;
        //        ((IMemoryBufferByteAccess)reference).GetBuffer(out pixelData, out capacity);

        //        // 直接写入内存
        //        int offset = (y * bitmap.PixelWidth + x) * 4;
        //        pixelData[offset] = color.B;     // Blue
        //        pixelData[offset + 1] = color.G; // Green
        //        pixelData[offset + 2] = color.R; // Red
        //        pixelData[offset + 3] = color.A; // Alpha
        //    }
        //    bitmap.Invalidate();
        //}

        public bool Equals(CanvasLayerData other) {
            return this.Tag == other.Tag;
        }

        public override bool Equals(object obj) {
            return Equals(obj as CanvasLayerData);
        }

        public override int GetHashCode() {
            return Tag.GetHashCode();
        }

        internal readonly int _width, _height;
        private string _filePath, _filePathForBitmap;//, _filePathForDarws, _filePathForImages;
        private readonly SemaphoreSlim _saveQueueLock = new(1, 1);
        private readonly TaskCompletionSource<bool> _renderCompleted = new();
        //private readonly BufferSaver<ObservableCollection<STAImage>> _imageSaver = new();
        //private readonly BufferSaver<ObservableCollection<STADraw>> _drawSaver = new();
    }
}
