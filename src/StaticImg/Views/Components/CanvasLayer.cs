using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.UIComponent.Converters;
using VirtualPaper.UIComponent.Utils;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Workloads.Creation.StaticImg.Models;

namespace Workloads.Creation.StaticImg.Views.Components {
    internal partial class CanvasLayer : Grid, IDisposable { // ui
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
                newDatas.OnRender += canvasLayer.NewDatas_OnRender;
                newDatas.OnDataLoaded += canvasLayer.Layer_OnDataLoaded;
            }
        }

        private void NewDatas_OnRender(object sender, EventArgs e) {

            _canvasControl.Invalidate();
        }

        public CanvasLayer() {
            _canvasControl = new() {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            this.Children.Add(_canvasControl);

            this.Unloaded += CanvasLayer_Unloaded;
        }

        private void CanvasLayer_Unloaded(object sender, RoutedEventArgs e) {
            Dispose();
        }

        private void Layer_OnDataLoaded(object sender, EventArgs e) {
            InitDataContext();
            LayerData.RenderCompleted.TrySetResult(true);
        }

        private void InitDataContext() {
            this.DataContext = LayerData;
            foreach (var bindingInfo in _cachedBindingInfos) {
                BindingsUtil.ApplyBindings(this, bindingInfo);
            }
        }
        
        //private void Shapes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
        //    //switch (e.Action) {
        //    //    case NotifyCollectionChangedAction.Add:
        //    //        foreach (var newItem in e.NewItems.Cast<VectorShapeBase>()) {
        //    //            AddShapeToVisualTree(newItem);
        //    //            //LayerData.ShapesStorage.Deltas.Add(new AddShapeDelta(newItem.Clone(), DateTime.Now));
        //    //        }
        //    //        break;

        //    //    case NotifyCollectionChangedAction.Remove:
        //    //        foreach (var oldItem in e.OldItems.Cast<VectorShapeBase>()) {
        //    //            RemoveShapeFromVisualTree(oldItem);
        //    //            //LayerData.ShapesStorage.Deltas.Add(new RemoveShapeDelta(oldItem.Id, DateTime.Now));
        //    //        }
        //    //        break;
        //    //}
        //}

        //private void AddShapeToVisualTree(VectorShapeBase shape) {
        //    var uiElement = shape.ToXamlShape();
        //    _shapeMap[shape] = uiElement;
        //    Children.Add(uiElement);
        //}

        //private void RemoveShapeFromVisualTree(VectorShapeBase shape) {
        //    if (_shapeMap.TryGetValue(shape, out var uiElement)) {
        //        Children.Remove(uiElement);
        //        _shapeMap.Remove(shape);
        //    }
        //}

        //private void RenderElement(BaseElement element) {
        //    if (element.Type == BaseElementType.Image) {
        //        var img = element as STAImage;
        //        Image imageControl = new() {
        //            Source = TypeConvertUtil.ByteArrayToImageSource(img.Data),
        //            Width = img.Width,
        //            Height = img.Height,
        //        };
        //        Canvas.SetLeft(imageControl, img.Position.X);
        //        Canvas.SetTop(imageControl, img.Position.Y);
        //        Canvas.SetZIndex(imageControl, img.ZIndex);
        //        this.Children.Add(imageControl);
        //    }
        //    else if (element.Type == BaseElementType.Draw) {
        //        var draw = element as STADraw;
        //        Path path = new() {
        //            Stroke = UintToSolidBrushConverter.HexToSolidBrush(draw.StrokeColor),
        //            StrokeThickness = draw.StrokeThickness,
        //            Data = TypeConvertUtil.CreatePathGeometry(draw.Points)
        //        };
        //        Canvas.SetLeft(path, 0);
        //        Canvas.SetTop(path, 0);
        //        Canvas.SetZIndex(path, draw.ZIndex);
        //        this.Children.Add(path);
        //    }
        //}

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

        //private void RemoveElement(BaseElement element) {
        //    if (element.Type == BaseElementType.Image) {
        //        var img = element as STAImage;
        //        var imageControl = this.Children.OfType<Image>().FirstOrDefault(i => Canvas.GetZIndex(i) == img.ZIndex);
        //        if (imageControl != null) {
        //            this.Children.Remove(imageControl);
        //        }
        //    }
        //    else if (element.Type == BaseElementType.Draw) {
        //        var draw = element as STADraw;
        //        var path = this.Children.OfType<Path>().FirstOrDefault(p => Canvas.GetZIndex(p) == draw.ZIndex);
        //        if (path != null) {
        //            this.Children.Remove(path);
        //        }
        //    }
        //}

        #region diapose
        private bool _isDisposed;
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_isDisposed) {
                return;
            }

            if (disposing) {
                if (LayerData != null) {
                    LayerData.OnRender += NewDatas_OnRender;
                    LayerData.OnDataLoaded += Layer_OnDataLoaded;
                }
                this.Children.Clear();
            }
            _isDisposed = true;
        }
        #endregion

        private readonly CanvasControl _canvasControl;
        //private const int ParallelThreshold = 1000; // 并行化的阈值        
        //private Image _image; // 像素管理
        //private Canvas _canvas; // 矢量管理
        // 在 CanvasLayer 类中定义
        //private readonly Dictionary<VectorShapeBase, UIElement> _shapeMap = [];
        //private readonly TaskCompletionSource<bool> _loadedTcs = new();
        private static readonly BindingInfo[] _cachedBindingInfos = [
            //new BindingInfo(BackgroundProperty, "Background", BindingMode.OneWay, new UintToSolidBrushConverter()),
            new BindingInfo(OpacityProperty, "Opacity", BindingMode.OneWay),
            new BindingInfo(VisibilityProperty, "IsEnable", BindingMode.OneWay, new BooleanToVisibilityConverter())
        ];
    }
}
