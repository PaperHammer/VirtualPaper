using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.Common;
using VirtualPaper.DraftPanel.Model.Runtime;
using VirtualPaper.DraftPanel.Utils;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;

namespace VirtualPaper.DraftPanel.Panels.Components {
    internal partial class LayerManager : Grid, IDisposable { // ui
        public LayerManagerData ManagerData {
            get { return (LayerManagerData)GetValue(ManagerDataProperty); }
            set { SetValue(ManagerDataProperty, value); }
        }
        public static readonly DependencyProperty ManagerDataProperty =
            DependencyProperty.Register("ManagerData", typeof(LayerManagerData), typeof(LayerManager), new PropertyMetadata(null, OnManagerDataChanged));

        private Point? _ponterPos;
        public Point? PointerPos {
            get { return _ponterPos; }
            set {
                if (_ponterPos == value) return;

                _ponterPos = value;
                PointF? formatPos = value == null ? null : TypeConvertUtil.FormatPoint(value, 0);
                ManagerData.PointerPosText = value == null ? string.Empty : $"{formatPos.Value.X}, {formatPos.Value.Y} {"像素"}";
            }
        }

        private static void OnManagerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var control = d as LayerManager;

            if (e.OldValue != null && e.NewValue != null) {
                throw new InvalidOperationException("ManagerData can only be set once.");
            }

            if (e.NewValue is LayerManagerData managerData) {
                managerData.LayersData.CollectionChanged += control.LayersData_CollectionChanged;
            }
        }

        public LayerManager() {
            InitProperty();
           
            this.Loading += LayerManager_Loading;
            this.PointerEntered += LayerManager_PointerEntered;
            this.PointerPressed += LayerManager_PointerPressed;
            this.PointerMoved += LayerManager_PointerMoved;
            this.PointerReleased += LayerManager_PointerReleased;
            this.PointerExited += LayerManager_PointerExited;
        }

        private void LayerManager_Loading(FrameworkElement sender, object args) {
            if (_isInitialized) {
                return;
            }

            InitDataContext();
            RenderBackground();
            //InitRenderAsync();
            _isInitialized = true;
        }

        private void InitProperty() {
            this.Margin = new Thickness(80);
            this.Background = new SolidColorBrush(Colors.Transparent);
            this.BorderThickness = new Thickness(1);
            this.BorderBrush = new SolidColorBrush(Colors.Gray);
        }

        private void InitDataContext() {
            this.DataContext = ManagerData;
            foreach (var bindingInfo in _cachedBindingInfos) {
                BindingsUtil.ApplyBindings(this, bindingInfo);
            }
        }

        private void RenderBackground() {
            _background.Width = this.Width;
            _background.Height = this.Height;
            Canvas.SetZIndex(_background, -1);
            this.Children.Add(_background);
            DrawGridBackground();
        }

        private void LayersData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            UpdateLayer(e);
        }

        private void UpdateLayer(NotifyCollectionChangedEventArgs e) {
            if (e.OldItems != null && e.OldItems.Count > 0)
                foreach (var layerData in e.OldItems)
                    RemoveElement(layerData as CanvasLayerData);
            if (e.NewItems != null && e.NewItems.Count > 0)
                foreach (var layerData in e.NewItems)
                    AddElement(layerData as CanvasLayerData);
        }

        private void AddElement(CanvasLayerData layerData) {
            var layer = new CanvasLayer() {
                LayerData = layerData,
                Width = this.Width,
                Height = this.Height,
            };
            _layerMap.Add(layerData, layer);
            this.Children.Add(layer);
        }

        private void RemoveElement(CanvasLayerData layerData) {
            this.Children.Remove(_layerMap[layerData]);
        }

        private void LayerManager_PointerExited(object sender, PointerRoutedEventArgs e) {
            _isDrawable = false;
            PointerPos = null;
            EndDrawing();
        }

        private void LayerManager_PointerReleased(object sender, PointerRoutedEventArgs e) {
            EndDrawing();
        }

        private void LayerManager_PointerMoved(object sender, PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(this);
            PointerPos = pointerPoint.Position;

            if (!_isDrawable || !_isDrawing) return;

            // 继续当前线条
            _currentLine.Points.Add(pointerPoint.Position);
            // 更新数据模型
            _currentDraw.Points.Add(new PointF((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y));
        }

        private void LayerManager_PointerPressed(object sender, PointerRoutedEventArgs e) {
            _isDrawable = ManagerData.SelectedLayerData.IsEnable;
            if (!_isDrawable) return;
            _isDrawing = true;

            // 开始新的线条
            var pointerPoint = e.GetCurrentPoint(this);
            _currentLine = new Polyline {
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _currentLine.Points.Add(pointerPoint.Position);
            _currentLine.Points.Add(pointerPoint.Position);
            // PolyLine 至少需要两个点才能显示内容。确保近单击时能显示绘制内容

            // 创建线条数据模型
            _currentDraw = new STADraw {
                StrokeColor = 0xFF000000,
                StrokeThickness = 2,
                Points = [new PointF((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y), new PointF((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y)],
                ZTime = DateTime.Now.Ticks
            };

            ManagerData.SelectedLayerData.AddDraw(_currentLine, _currentDraw);
        }

        private void LayerManager_PointerEntered(object sender, PointerRoutedEventArgs e) {
            if (ManagerData.SelectedLayerData == null) {
                Draft.Instance.GetNotify().ShowMsg(true, nameof(Constants.I18n.Draft_SI_LayerNotAvailable), InfoBarType.Error, nameof(Constants.I18n.Draft_SI_LayerNotAvailable), false);
                return;
            }
            if (!ManagerData.SelectedLayerData.IsEnable) {
                Draft.Instance.GetNotify().ShowMsg(true, nameof(Constants.I18n.Draft_SI_LayerLocked), InfoBarType.Warning, nameof(Constants.I18n.Draft_SI_LayerLocked), false);
                return;
            }
        }

        private void EndDrawing() {
            if (_isDrawing) {
                _isDrawing = false;
                _currentLine = null; // 清除当前线条引用
                _currentDraw = null;

                ManagerData.SelectedLayerData.DrawsChanged();
            }
        }

        private void DrawGridBackground() {
            // 清除旧的背景
            _background.Children.Clear();

            // 定义网格间距
            int gridSize = 10;
            // 定义两种颜色
            var color1 = Colors.LightGray; // 浅灰色
            var color2 = Colors.DarkGray;  // 深灰色

            // 创建两个 GeometryGroup 分别存储浅色和深色方块
            var lightGeometryGroup = new GeometryGroup();
            var darkGeometryGroup = new GeometryGroup();

            for (int x = 0; x < this.Width; x += gridSize) {
                for (int y = 0; y < this.Height; y += gridSize) {
                    bool isLight = ((x / gridSize) + (y / gridSize)) % 2 == 0;

                    // 创建矩形几何图形
                    var rectangleGeometry = new RectangleGeometry() { Rect = new Rect(x, y, gridSize, gridSize) };

                    if (isLight) {
                        lightGeometryGroup.Children.Add(rectangleGeometry);
                    }
                    else {
                        darkGeometryGroup.Children.Add(rectangleGeometry);
                    }
                }
            }

            // 创建 Points 对象用于绘制浅色方块
            var lightPath = new Path {
                Fill = new SolidColorBrush(color1),
                Data = lightGeometryGroup,
            };

            // 创建 Points 对象用于绘制深色方块
            var darkPath = new Path {
                Fill = new SolidColorBrush(color2),
                Data = darkGeometryGroup,
            };

            _background.Children.Add(lightPath);
            _background.Children.Add(darkPath);
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
                this.Loading -= LayerManager_Loading;
                this.PointerEntered -= LayerManager_PointerEntered;
                this.PointerPressed -= LayerManager_PointerPressed;
                this.PointerMoved -= LayerManager_PointerMoved;
                this.PointerReleased -= LayerManager_PointerReleased;
                this.PointerExited -= LayerManager_PointerExited;
                if (ManagerData.LayersData != null) {
                    ManagerData.LayersData.CollectionChanged -= LayersData_CollectionChanged;
                }

                this.Children.Clear();
            }

            // 非托管资源清理

            _isDisposed = true;
        }
        #endregion

        private bool _isDisposed;
        private bool _isInitialized;
        private readonly Canvas _background = new();
        private readonly Dictionary<CanvasLayerData, CanvasLayer> _layerMap = [];
        private bool _isDrawable = false, _isDrawing = false;
        private Polyline _currentLine; // 当前正在绘制的线条
        private STADraw _currentDraw;  // 当前线条的数据模型
        private static readonly BindingInfo[] _cachedBindingInfos = [
            new BindingInfo(WidthProperty, "Size.Width", BindingMode.OneWay),
            new BindingInfo(HeightProperty, "Size.Height", BindingMode.OneWay),
        ];
    }
}
