using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.UIComponent.Converters;
using VirtualPaper.UIComponent.Utils;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.EventArg;
using Workloads.Creation.StaticImg.Utils;

namespace Workloads.Creation.StaticImg.Views.Components {
    internal partial class CanvasLayer : Canvas, IDisposable { // ui
        public CanvasLayerData LayerData {
            get { return (CanvasLayerData)GetValue(LayerDataProperty); }
            set { SetValue(LayerDataProperty, value); }
        }
        public static readonly DependencyProperty LayerDataProperty =
            DependencyProperty.Register("LayerData", typeof(CanvasLayerData), typeof(CanvasLayer), new PropertyMetadata(null, OnLayerDataChanged));

        private static void OnLayerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var canvasLayer = d as CanvasLayer;
            if (canvasLayer == null) return;

            if (e.OldValue != null && e.NewValue != null) {
                throw new InvalidOperationException("LayerData can only be set once.");
            }

            if (e.NewValue is CanvasLayerData newDatas) {
                newDatas.OnDrawsChanging += canvasLayer.Layer_OnDrawsChanging;
                newDatas.OnDrawsChanged += canvasLayer.Layer_OnDrawsChanged;
                newDatas.OnDataLoaded += canvasLayer.NewDatas_OnDataLoaded;
            }
        }

        public CanvasLayer() {
            this.Loading += CanvasLayer_Loading;
            this.Loaded += CanvasLayer_Loaded;
            this.Unloaded += CanvasLayer_Unloaded;
        }

        private void CanvasLayer_Loaded(object sender, RoutedEventArgs e) {
            _loadedTcs.TrySetResult(true);
        }

        private void CanvasLayer_Unloaded(object sender, RoutedEventArgs e) {
            Dispose();
        }

        private void CanvasLayer_Loading(FrameworkElement sender, object args) {
            if (_isInitialized) {
                return;
            }

            InitDataContext();

            _isInitialized = true;
        }

        private void InitDataContext() {
            this.DataContext = LayerData;
            foreach (var bindingInfo in _cachedBindingInfos) {
                BindingsUtil.ApplyBindings(this, bindingInfo);
            }
        }

        //  合并图片和线条：
        //      将 Images 和 Draws 中的元素合并到一个动态列表 allElements 中。
        //      每个元素包含 ZIndex、ZTime、实际数据对象（Element），以及类型标识（Type）。
        //  排序规则：
        //      使用 OrderBy 对 ZIndex 进行升序排序。
        //      使用 ThenBy 对 ZTime 进行升序排序。
        //      这样可以确保：
        //          不同 ZIndex 的元素按照层级从低到高渲染。
        //          相同 ZIndex 的元素按照写入时间的先后顺序渲染。
        //  渲染逻辑：
        //      遍历排序后的元素列表。
        //      根据 Type 判断是图片还是线条，并分别创建对应的控件（STAImage 或 Points）。
        //      设置控件的位置、大小、ZIndex 等属性，并添加到 Layer.Children 中。
        private async Task InitRenderAsync() {
            await _loadedTcs.Task;

            // 创建一个包含所有元素的列表
            IEnumerable<BaseElement> allElements = LayerData.Images.Cast<BaseElement>().Concat(LayerData.Draws);

            // 按照 ZIndex 升序，ZTime 升序排序
            var sortedElements = allElements.Count() > ParallelThreshold
                ? allElements.AsParallel()
                    .OrderBy(e => e.ZIndex) // 先按 ZIndex 排序
                    .ThenBy(e => e.ZTime)  // 再按 ZTime 排序
                    .ToList()
                : [.. allElements
                    .OrderBy(e => e.ZIndex)
                    .ThenBy(e => e.ZTime)];

            // 根据排序结果渲染
            foreach (var element in sortedElements) {
                RenderElement(element);
            }

            LayerData.LayerThum = await GenerateThumbnailAsync(this, Consts.LayerThumWidth, Consts.LayerThumHeight);
            LayerData.RenderCompleted.TrySetResult(true);
        }

        private void Layer_OnDrawsChanging(object sender, PolylineEventArgs e) {
            switch (e.Operation) {
                case OperationType.Add:
                    this.Children.Add(e.Polyline);
                    break;
                case OperationType.Remove:
                    this.Children.Remove(e.Polyline);
                    break;
                default:
                    break;
            }
        }

        private async void Layer_OnDrawsChanged(object sender, EventArgs e) {
            LayerData.LayerThum = await GenerateThumbnailAsync(this, Consts.LayerThumWidth, Consts.LayerThumHeight);
        }

        private async void NewDatas_OnDataLoaded(object sender, EventArgs e) {
            await InitRenderAsync();
        }

        private void RenderElement(BaseElement element) {
            if (element.Type == BaseElementType.Image) {
                var img = element as STAImage;
                Image imageControl = new() {
                    Source = TypeConvertUtil.ByteArrayToImageSource(img.Data),
                    Width = img.Width,
                    Height = img.Height,
                };
                Canvas.SetLeft(imageControl, img.Position.X);
                Canvas.SetTop(imageControl, img.Position.Y);
                Canvas.SetZIndex(imageControl, img.ZIndex);
                this.Children.Add(imageControl);
            }
            else if (element.Type == BaseElementType.Draw) {
                var draw = element as STADraw;
                Path path = new() {
                    Stroke = UintToSolidBrushConverter.HexToSolidBrush(draw.StrokeColor),
                    StrokeThickness = draw.StrokeThickness,
                    Data = TypeConvertUtil.CreatePathGeometry(draw.Points)
                };
                Canvas.SetLeft(path, 0);
                Canvas.SetTop(path, 0);
                Canvas.SetZIndex(path, draw.ZIndex);
                this.Children.Add(path);
            }
        }

        private async Task<ImageSource> GenerateThumbnailAsync(UIElement element, double thumbnailWidth, double thumbnailHeight) {
            // 使用 RenderTargetBitmap 捕获整个画布的内容
            var renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(element);

            // 获取捕获的像素数据
            var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
            var width = renderTargetBitmap.PixelWidth;
            var height = renderTargetBitmap.PixelHeight;

            // 创建 SoftwareBitmap 并加载原始像素数据
            var softwareBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8,
                width,
                height,
                BitmapAlphaMode.Premultiplied);

            softwareBitmap.CopyFromBuffer(pixelBuffer);

            // 使用 BitmapEncoder 进行高质量缩放
            var resizedStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, resizedStream);

            encoder.SetSoftwareBitmap(softwareBitmap);
            encoder.BitmapTransform.ScaledWidth = (uint)thumbnailWidth;
            encoder.BitmapTransform.ScaledHeight = (uint)thumbnailHeight;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant; // 高质量插值

            await encoder.FlushAsync();

            // 将缩放后的图像转换为 ImageSourceKey
            var bitmapImage = new BitmapImage();
            resizedStream.Seek(0);
            await bitmapImage.SetSourceAsync(resizedStream);

            return bitmapImage;
        }

        private void RemoveElement(BaseElement element) {
            if (element.Type == BaseElementType.Image) {
                var img = element as STAImage;
                var imageControl = this.Children.OfType<Image>().FirstOrDefault(i => Canvas.GetZIndex(i) == img.ZIndex);
                if (imageControl != null) {
                    this.Children.Remove(imageControl);
                }
            }
            else if (element.Type == BaseElementType.Draw) {
                var draw = element as STADraw;
                var path = this.Children.OfType<Path>().FirstOrDefault(p => Canvas.GetZIndex(p) == draw.ZIndex);
                if (path != null) {
                    this.Children.Remove(path);
                }
            }
        }

        #region diapose
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this); // 避免重复调用 Finalizer
        }

        protected virtual void Dispose(bool disposing) {
            if (_isDisposed) {
                return;
            }

            if (disposing) {
                this.Loading -= CanvasLayer_Loading;
                this.Unloaded -= CanvasLayer_Unloaded;
                if (LayerData != null) {
                    LayerData.OnDrawsChanging -= this.Layer_OnDrawsChanging;
                }

                this.Children.Clear();
            }

            // 非托管资源清理

            _isDisposed = true;
        }
        #endregion

        private const int ParallelThreshold = 1000; // 并行化的阈值
        private bool _isDisposed;
        private bool _isInitialized;
        private readonly TaskCompletionSource<bool> _loadedTcs = new();
        private static readonly BindingInfo[] _cachedBindingInfos = [
            new BindingInfo(BackgroundProperty, "Background", BindingMode.OneWay, new UintToSolidBrushConverter()),
            new BindingInfo(OpacityProperty, "Opacity", BindingMode.OneWay),
            new BindingInfo(VisibilityProperty, "IsEnable", BindingMode.OneWay, new BooleanToVisibilityConverter())
        ];
    }
}
