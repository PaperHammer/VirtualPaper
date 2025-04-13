using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.Mvvm;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Models.EventArg;
using Workloads.Creation.StaticImg.Models.VectorShapes;

namespace Workloads.Creation.StaticImg.Models {
    [JsonSerializable(typeof(CanvasLayerData))]
    internal partial class CanvasLayerDataContext : JsonSerializerContext { }

    internal partial class CanvasLayerData : ObservableObject, IEquatable<CanvasLayerData> {
        public event EventHandler OnDataLoaded;
        public event EventHandler<PathEventArgs> OnPathAdding;
        public event EventHandler<PathEventArgs> OnPathChanged;
        public event EventHandler OnRender;

        [JsonIgnore]
        public TaskCompletionSource<bool> RenderCompleted => _renderCompleted;

        private string _name = string.Empty;
        public string Name {
            get => _name;
            set { if (_name == value) return; _name = value; OnPropertyChanged(); }
        }

        public long Tag { get; }
        public bool IsRootBackground { get; }

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

        //[JsonIgnore]
        //public SpatialIndex<VectorShapeBase> SpatialsIndex { get; } = new();

        //[JsonIgnore]
        //public ObservableList<VectorShapeBase> Shapes { get; private set; } = [];

        //[JsonIgnore]
        //internal ShapesStorageModel ShapesStorage = new();

        //internal class ShapesStorageModel {
        //    public byte[] FullData { get; set; }
        //    public List<ShapeDelta> Deltas { get; set; } = [];
        //    public DateTime LastFullSave { get; set; }
        //}

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
            //InitBitmap();
        }

        //private async void InitBitmap() {
        //    //BitmapData = new(_width, _height);
        //    //if (IsRootBackground) {
        //    //    await WriteableBitmapExtension.FillWithWhiteAsync(BitmapData);
        //    //}
        //}

        public void Render() => OnRender?.Invoke(this, EventArgs.Empty);

        public async Task<CanvasLayerData> CopyAsync() {
            CanvasLayerData newLayerData = new(_filePath, _width, _height) {
                //Background = this.Background,
                //BitmapData = await this.BitmapData.DeepCopyAsync(),
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

                //await WriteableBitmapExtension.WriteableBitmapToFileAsync(BitmapData, _filePathForBitmap);

                //await MessagePackSaver.SaveAsync<ObservableList<VectorShapeBase>>(_filePathForData, Shapes);
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

                //BitmapData = await WriteableBitmapExtension.ReadWriteableBitmapFromFileAsync(_filePathForBitmap);

                //Shapes = await MessagePackSaver.LoadAsync<ObservableList<VectorShapeBase>>(_filePathForData) ?? [];
                //foreach (var shape in Shapes) {
                //    SpatialsIndex.Insert(GetShapeBounds(shape), shape);
                //}

                OnDataLoaded?.Invoke(this, EventArgs.Empty);

                //await RenderCompleted.Task;
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Project_STI_LayerLoadFailed), InfoBarType.Error, Name, Tag.ToString(), false);
            }
        }

        public void AddShape(VectorShapeBase shape) {
            //Shapes.Add(shape);
            //SpatialsIndex.Insert(GetShapeBounds(shape), shape);
        }
        
        public void RemoveShape(VectorShapeBase shape) {
            //Shapes.Remove(shape);
            //SpatialsIndex.(GetShapeBounds(shape), shape);
        }
        
        public void InsertShape(int pos, VectorShapeBase shape) {
            //Shapes.Insert(pos, shape);
            //SpatialsIndex.Insert(GetShapeBounds(shape), shape);
        }

        //private static Rect GetShapeBounds(VectorShapeBase shape) {
        //    // 获取原始几何边界
        //    var bounds = shape.GeometryData.Bounds;

        //    // 如果没有变换，直接返回
        //    if (shape.RenderTransform == null)
        //        return bounds;

        //    // 获取变换矩阵
        //    Matrix3x2 transform = GetTransformMatrix(shape.RenderTransform);

        //    // 手动变换Rect的四个角点
        //    var corners = new[] {
        //        new Point(bounds.Left, bounds.Top),
        //        new Point(bounds.Right, bounds.Top),
        //        new Point(bounds.Right, bounds.Bottom),
        //        new Point(bounds.Left, bounds.Bottom)
        //    };

        //    // 计算变换后的边界
        //    return TransformBoundingBox(corners, transform);
        //}

        //// 获取变换矩阵
        //private static Matrix3x2 GetTransformMatrix(Transform transform) {
        //    return transform switch {
        //        MatrixTransform mt => new Matrix3x2(
        //            (float)mt.Matrix.M11, (float)mt.Matrix.M12,
        //            (float)mt.Matrix.M21, (float)mt.Matrix.M22,
        //            (float)mt.Matrix.OffsetX, (float)mt.Matrix.OffsetY),
        //        RotateTransform rt => Matrix3x2.CreateRotation(
        //            (float)(rt.Angle * Math.PI / 180)),
        //        ScaleTransform st => Matrix3x2.CreateScale(
        //            (float)st.ScaleX, (float)st.ScaleY),
        //        TranslateTransform tt => Matrix3x2.CreateTranslation(
        //            (float)tt.X, (float)tt.Y),
        //        CompositeTransform ct => Matrix3x2.CreateTranslation((float)ct.TranslateX, (float)ct.TranslateY) *
        //                               Matrix3x2.CreateRotation((float)(ct.Rotation * Math.PI / 180)) *
        //                               Matrix3x2.CreateScale((float)ct.ScaleX, (float)ct.ScaleY),
        //        _ => Matrix3x2.Identity
        //    };
        //}

        //// 计算变换后边界
        //private static Rect TransformBoundingBox(Point[] corners, Matrix3x2 transform) {
        //    float minX = float.MaxValue, minY = float.MaxValue;
        //    float maxX = float.MinValue, maxY = float.MinValue;

        //    foreach (var corner in corners) {
        //        var transformed = Vector2.Transform(
        //            new Vector2((float)corner.X, (float)corner.Y),
        //            transform);

        //        minX = Math.Min(minX, transformed.X);
        //        minY = Math.Min(minY, transformed.Y);
        //        maxX = Math.Max(maxX, transformed.X);
        //        maxY = Math.Max(maxY, transformed.Y);
        //    }

        //    return new Rect(minX, minY, maxX - minX, maxY - minY);
        //}

        //internal void PathAdd(Microsoft.UI.Xaml.Shapes.Path path) {
        //    //OnPathAdding?.Invoke(this, new(path, OperationType.Add));
        //}

        ////internal void RemovePath(Microsoft.UI.Xaml.Shapes.Path path) {
        ////    OnPathAdding?.Invoke(this, new(path, OperationType.Remove));
        ////}

        //internal async void PathDone(Microsoft.UI.Xaml.Shapes.Path path) {
        //    //// render TODO
        //    //// 创建 WriteableBitmap
        //    //var renderTargetBitmap = new RenderTargetBitmap();
        //    //var pathWidth = (int)path.ActualWidth;
        //    //var pathHeight = (int)path.ActualHeight;

        //    //// 渲染 Path 到 RenderTargetBitmap
        //    //await renderTargetBitmap.RenderAsync(path);

        //    //// 获取像素数据
        //    //var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
        //    //var pixels = pixelBuffer.ToArray();

        //    //// 创建 WriteableBitmap 并写入像素数据
        //    //using (var stream = BitmapData.PixelBuffer.AsStream()) {
        //    //    await stream.WriteAsync(pixels);
        //    //}

        //    //OnPathChanged?.Invoke(this, new(path, OperationType.Remove));
        //}

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
            _filePathForData = System.IO.Path.Combine(newFolderPath, Tag + ".data");
            //_filePathForDarws = System.IO.Path.Combine(newFolderPath, Tag + ".draws");
            //_filePathForImages = System.IO.Path.Combine(newFolderPath, Tag + ".images");
        }

        internal async Task DeletAsync() {
            await FileUtil.TryDeleteFileAsync(_filePathForData, 0, 0);
            //await FileUtil.TryDeleteFileAsync(_filePathForDarws, 0, 0);
            //await FileUtil.TryDeleteFileAsync(_filePathForImages, 0, 0);
        }

        //public async Task RenderAsync(int x, int y, Color color, double brushSize) {
        //    //double radius = brushSize / 2;
        //    //int radiusInt = (int)Math.Ceiling(radius);
        //    //int radiusSquared = (int)(radius * radius);

        //    //// 边界裁剪
        //    //int startX = Math.Max(0, x - radiusInt);
        //    //int endX = Math.Min(BitmapData.PixelWidth - 1, x + radiusInt);
        //    //int startY = Math.Max(0, y - radiusInt);
        //    //int endY = Math.Min(BitmapData.PixelHeight - 1, y + radiusInt);

        //    //if (brushSize > 20) {
        //    //    // 当 brushSize > 20 时启用并行化
        //    //    RenderParallel(startY, endY, startX, endX, x, y, color, radiusSquared);
        //    //}
        //    //else {
        //    //    // 普通渲染（利用对称性）
        //    //    RenderSymmetric(x, y, color, radiusInt, radiusSquared);
        //    //}

        //    //BitmapData.Invalidate();
        //}

        //private void RenderSymmetric(int x, int y, Color color, int radiusInt, int radiusSquared) {
        //    for (int dy = 0; dy <= radiusInt; dy++) {
        //        for (int dx = 0; dx <= radiusInt; dx++) {
        //            if (dx * dx + dy * dy <= radiusSquared) {
        //                // 映射到8个象限
        //                _ = SetPixelAsync(x + dx, y + dy, color); // 第一象限
        //                _ = SetPixelAsync(x - dx, y + dy, color); // 第二象限
        //                _ = SetPixelAsync(x + dx, y - dy, color); // 第四象限
        //                _ = SetPixelAsync(x - dx, y - dy, color); // 第三象限

        //                // 如果不在轴上，还要考虑水平和垂直对称
        //                if (dx != 0 && dy != 0) {
        //                    _ = SetPixelAsync(x + dy, y + dx, color); // 对角线对称
        //                    _ = SetPixelAsync(x - dy, y + dx, color);
        //                    _ = SetPixelAsync(x + dy, y - dx, color);
        //                    _ = SetPixelAsync(x - dy, y - dx, color);
        //                }
        //            }
        //        }
        //    }
        //}

        //private void RenderParallel(int startY, int endY, int startX, int endX, int x, int y, Color color, int radiusSquared) {
        //    // 使用并行化处理
        //    Task.Run(() => {
        //        Parallel.For(startY, endY + 1, py => {
        //            for (int px = startX; px <= endX; px++) {
        //                int dx = px - x;
        //                int dy = py - y;

        //                if (dx * dx + dy * dy <= radiusSquared) {
        //                    _ = SetPixelAsync(px, py, color);
        //                }
        //            }
        //        });
        //    });
        //}

        //private async Task SetPixelAsync(int x, int y, Color color) {
        //    using (var stream = BitmapData.PixelBuffer.AsStream()) {
        //        int bytesPerPixel = 4;
        //        int stride = BitmapData.PixelWidth * bytesPerPixel;
        //        int offset = y * stride + x * bytesPerPixel;

        //        if (offset < 0 || offset >= stream.Length) {
        //            return;
        //        }

        //        stream.Position = offset;

        //        byte[] pixelBytes = new byte[bytesPerPixel];
        //        pixelBytes[0] = color.B;
        //        pixelBytes[1] = color.G;
        //        pixelBytes[2] = color.R;
        //        pixelBytes[3] = color.A;

        //        await stream.WriteAsync(pixelBytes);
        //    }
        //}









        //public async Task RenderAsync(int x, int y, Color color, double brushSize) {
        //    double radius = brushSize / 2;
        //    int radiusInt = (int)Math.Ceiling(radius);
        //    int radiusSquared = (int)(radius * radius);

        //    int startX = Math.Max(0, x - radiusInt);
        //    int endX = Math.Min(_width - 1, x + radiusInt);
        //    int startY = Math.Max(0, y - radiusInt);
        //    int endY = Math.Min(_height - 1, y + radiusInt);

        //    using (var stream = BitmapData.PixelBuffer.AsStream()) {
        //        byte[] pixelBuffer = new byte[stream.Length];
        //        stream.Read(pixelBuffer, 0, pixelBuffer.Length);

        //        Parallel.For(startY, endY + 1, py => {
        //            for (int px = startX; px <= endX; px++) {
        //                int dx = px - x;
        //                int dy = py - y;

        //                if (dx * dx + dy * dy <= radiusSquared) {
        //                    int offset = (py * _width + px) * 4;
        //                    pixelBuffer[offset] = color.B;
        //                    pixelBuffer[offset + 1] = color.G;
        //                    pixelBuffer[offset + 2] = color.R;
        //                    pixelBuffer[offset + 3] = color.A;
        //                }
        //            }
        //        });

        //        stream.Position = 0;
        //        await stream.WriteAsync(pixelBuffer);
        //    }

        //    BitmapData.Invalidate();
        //}











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
        private string _filePath, _filePathForData;//, _filePathForDarws, _filePathForImages;
        private readonly SemaphoreSlim _saveQueueLock = new(1, 1);
        private readonly TaskCompletionSource<bool> _renderCompleted = new();
        //private readonly BufferSaver<ObservableCollection<STAImage>> _imageSaver = new();
        //private readonly BufferSaver<ObservableCollection<STADraw>> _drawSaver = new();
    }
}
