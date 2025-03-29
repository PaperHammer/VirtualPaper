using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.DraftPanel.Model.EventArg;
using VirtualPaper.DraftPanel.Model.Runtime;
using VirtualPaper.DraftPanel.Utils;
using VirtualPaper.UIComponent.Converters;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.DraftPanel.Panels.Components {
    internal partial class CanvasLayer : Canvas, IDisposable { // ui
        public CanvasLayerData LayerData {
            get { return (CanvasLayerData)GetValue(LayerDataProperty); }
            set { SetValue(LayerDataProperty, value); }
        }
        public static readonly DependencyProperty LayerDataProperty =
            DependencyProperty.Register("LayerData", typeof(CanvasLayerData), typeof(CanvasLayer), new PropertyMetadata(null, OnLayerDataChanged));

        //public ObservableCollection<STAImage> Images {
        //    get { return (ObservableCollection<STAImage>)GetValue(ImagesProperty); }
        //    set { SetValue(ImagesProperty, value); }
        //}
        //public static readonly DependencyProperty ImagesProperty =
        //    DependencyProperty.Register("Images", typeof(ObservableCollection<STAImage>), typeof(CanvasLayer), new PropertyMetadata(null, OnIamgesPropertyChanged));

        //public ObservableCollection<STADraw> Draws {
        //    get { return (ObservableCollection<STADraw>)GetValue(DrawsProperty); }
        //    set { SetValue(DrawsProperty, value); }
        //}
        //public static readonly DependencyProperty DrawsProperty =
        //    DependencyProperty.Register("Draws", typeof(ObservableCollection<STADraw>), typeof(CanvasLayer), new PropertyMetadata(null, OnDrawsPropertyChanged));

        private static void OnLayerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var canvasLayer = d as CanvasLayer;
            if (canvasLayer == null) return;

            if (e.OldValue != null && e.NewValue != null) {
                throw new InvalidOperationException("LayerData can only be set once.");
            }

            if (e.NewValue is CanvasLayerData newDatas) {
                //newDatas.OnImagesCollectionChanged += canvasLayer.STAData_CollectionChanged;
                //newDatas.OnDrawssCollectionChanged += canvasLayer.STAData_CollectionChanged;
                newDatas.OnDrawsChanged += canvasLayer.Layer_OnDrawsChanged;
                newDatas.OnDataLoaded += canvasLayer.NewDatas_OnDataLoaded;
            }
        }

        private void NewDatas_OnDataLoaded(object sender, EventArgs e) {
            InitRender();
        }

        //private static void OnDrawsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        //    var control = d as CanvasLayer;
        //    if (e.OldValue is ObservableCollection<STADraw> oldDraws) {
        //        oldDraws.CollectionChanged -= control.STAData_CollectionChanged;
        //    }

        //    if (e.NewValue is ObservableCollection<STADraw> newDraws) {
        //        newDraws.CollectionChanged += control.STAData_CollectionChanged;
        //    }
        //}

        //private static void OnIamgesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        //    var control = d as CanvasLayer;
        //    if (e.OldValue is ObservableCollection<STAImage> oldIamges) {
        //        oldIamges.CollectionChanged -= control.STAData_CollectionChanged;
        //    }

        //    if (e.NewValue is ObservableCollection<STAImage> newIamges) {
        //        newIamges.CollectionChanged += control.STAData_CollectionChanged;
        //    }
        //}

        public CanvasLayer() {
            this.Loading += CanvasLayer_Loading;
            this.Unloaded += CanvasLayer_Unloaded;
        }

        private void CanvasLayer_Unloaded(object sender, RoutedEventArgs e) {
            Dispose();
        }

        private void CanvasLayer_Loading(FrameworkElement sender, object args) {
            if (_isInitialized) {
                return;
            }

            InitDataContext();
            //InitRender();
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
        private void InitRender() {
            // 创建一个包含所有元素的列表
            IEnumerable<StaticImgElement> allElements = LayerData.Images.Cast<StaticImgElement>().Concat(LayerData.Draws);

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
        }

        //private void STAData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
        //    //UpdateRender(e);
        //}

        private void Layer_OnDrawsChanged(object sender, PolylineEventArgs e) {
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

        //private void UpdateRender(NotifyCollectionChangedEventArgs e) {
        //    if (e.OldItems != null && e.OldItems.Count > 0)
        //        RemoveElement(e.OldItems[0] as StaticImgElement);
        //    if (e.NewItems != null && e.NewItems.Count > 0)
        //        RenderElement(e.NewItems[0] as StaticImgElement);
        //}

        private void RenderElement(StaticImgElement element) {
            if (element.Type == StaticImgElementType.Image) {
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
            else if (element.Type == StaticImgElementType.Draw) {
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

        //private void RemoveElement(StaticImgElement element) {
        //    if (element.Type == StaticImgElementType.Image) {
        //        var img = element as STAImage;
        //        var imageControl = this.Children.OfType<Image>().FirstOrDefault(i => Canvas.GetZIndex(i) == img.ZIndex);
        //        if (imageControl != null) {
        //            this.Children.Remove(imageControl);
        //        }
        //    }
        //    else if (element.Type == StaticImgElementType.Draw) {
        //        var draw = element as STADraw;
        //        var path = this.Children.OfType<Path>().FirstOrDefault(p => Canvas.GetZIndex(p) == draw.ZIndex);
        //        if (path != null) {
        //            this.Children.Remove(path);
        //        }
        //    }
        //}

        public CanvasLayer Copy() {
            CanvasLayer newLayer = new() {
                LayerData = this.LayerData.Copy()
            };
            return newLayer;
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
                    LayerData.OnDrawsChanged -= this.Layer_OnDrawsChanged;
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
        private static readonly BindingInfo[] _cachedBindingInfos = [
            new BindingInfo(BackgroundProperty, "Background", BindingMode.OneWay, new UintToSolidBrushConverter()),
            new BindingInfo(OpacityProperty, "Opacity", BindingMode.OneWay),
            new BindingInfo(VisibilityProperty, "IsEnable", BindingMode.OneWay, new BooleanToVisibilityConverter())
        ];
    }
}
