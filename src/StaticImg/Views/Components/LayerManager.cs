using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Converters;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.ToolItemUtil;
using Workloads.Creation.StaticImg.Utils;

namespace Workloads.Creation.StaticImg.Views.Components {
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
                control._tool = new ToolManager();
                control._tool.RegisterTool(ToolType.PaintBrush, new PaintBrushTool(managerData));
                control._tool.RegisterTool(ToolType.Fill, new FillTool(managerData));
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
            RenderWorkerground();
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

        private void RenderWorkerground() {
            _workerground.Width = this.Width;
            _workerground.Height = this.Height;
            Canvas.SetZIndex(_workerground, -1);
            this.Children.Add(_workerground);
            DrawGridWorkerground();
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
            _layerMap.TryAdd(layerData, layer);
            Canvas.SetZIndex(layer, layerData.ZIndex);
            this.Children.Add(layer);
        }

        private void RemoveElement(CanvasLayerData layerData) {
            this.Children.Remove(_layerMap[layerData]);
        }

        private void LayerManager_PointerExited(object sender, PointerRoutedEventArgs e) {
            //_isDrawable = false;
            PointerPos = null;
            //EndDrawing();
            HandleToolEvent(tool => tool.OnPointerExited(new(e, e.GetCurrentPoint(this))));

            if (OriginalInputCursor != null) {
                this.ProtectedCursor = OriginalInputCursor;
            }
        }

        private void LayerManager_PointerReleased(object sender, PointerRoutedEventArgs e) {
            //EndDrawing();
            HandleToolEvent(tool => tool.OnPointerReleased(new(e, e.GetCurrentPoint(this))));
        }

        private void LayerManager_PointerMoved(object sender, PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(this);
            PointerPos = pointerPoint.Position;

            HandleToolEvent(tool => tool.OnPointerMoved(new(e, e.GetCurrentPoint(this))));
            //if (!_isDrawable || !_isDrawing) return;

            //// 更新 PathGeometry
            //var lineSegment = new LineSegment { Point = pointerPoint.Position };
            //_pathGeometry.Figures[0].Segments.Add(lineSegment);

            //// 更新数据模型
            //_currentDraw.Points.Add(new PointF((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y));
        }

        private void LayerManager_PointerPressed(object sender, PointerRoutedEventArgs e) {
            HandleToolEvent(tool => tool.OnPointerPressed(new(e, e.GetCurrentPoint(this))));
            //if (!_isDrawable) return;
            //_isDrawing = true;

            //// 开始新的线条
            //var pointerPoint = e.GetCurrentPoint(this);
            //var color = pointerPoint.Properties.IsRightButtonPressed ?
            //    ManagerData.BackgroundColor : ManagerData.ForegroundColor;

            //var pathColor = new SolidColorBrush(UintColor.MixAlpha(color, ManagerData.BrushOpacity / 100.0));
            //// 创建 Path 和 PathGeometry
            //_pathGeometry = new PathGeometry();
            //_currentPath = new Path {
            //    Stroke = pathColor,
            //    StrokeThickness = ManagerData.BrushThickness,
            //    StrokeLineJoin = PenLineJoin.Round,
            //    StrokeStartLineCap = PenLineCap.Round,
            //    StrokeEndLineCap = PenLineCap.Round,
            //    Data = _pathGeometry
            //};

            //// 创建初始点
            //var startPoint = pointerPoint.Position;
            //var figure = new PathFigure { StartPoint = startPoint };
            //_pathGeometry.Figures.Add(figure);

            //// 创建线条数据模型
            //_currentDraw = new STADraw {
            //    StrokeColor = UintToSolidBrushConverter.ColorToHex(pathColor.Color),
            //    StrokeThickness = ManagerData.BrushThickness,
            //    Points = [new PointF((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y), new PointF((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y)],
            //    ZTime = DateTime.Now.Ticks
            //};

            //ManagerData.SelectedLayerData.AddDraw(_currentPath, _currentDraw);
        }

        private void LayerManager_PointerEntered(object sender, PointerRoutedEventArgs e) {
            OriginalInputCursor = this.ProtectedCursor ?? InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            this.ProtectedCursor = ManagerData.Cursor;

            if (ManagerData.SelectedToolType != ToolType.PaintBrush) return;

            if (ManagerData.SelectedLayerData == null) {
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Draft_SI_LayerNotAvailable), InfoBarType.Error, key: nameof(Constants.I18n.Draft_SI_LayerNotAvailable), isAllowDuplication: false);
                return;
            }

            if (!ManagerData.SelectedLayerData.IsEnable) {
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Draft_SI_LayerLocked), InfoBarType.Warning, key: nameof(Constants.I18n.Draft_SI_LayerLocked), isAllowDuplication: false);
                return;
            }

            HandleToolEvent(tool => tool.OnPointerEntered(new(e, e.GetCurrentPoint(this))));
            //_isDrawable = ManagerData.SelectedLayerData.IsEnable;
        }

        //private void EndDrawing() {
        //    if (_isDrawing) {
        //        _isDrawing = false;
        //        //_currentLine = null; // 清除当前线条引用
        //        _currentPath = null;
        //        _pathGeometry = null;
        //        _currentDraw = null;

        //        ManagerData.SelectedLayerData.DrawsChanged();
        //    }
        //}

        private void HandleToolEvent(Action<ITool> action) {
            var selectedTool = _tool.GetTool(ManagerData.SelectedToolType);
            if (selectedTool != null) {
                action(selectedTool);
            }
        }

        private void DrawGridWorkerground() {
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

            _workerground.Children.Add(lightPath);
            _workerground.Children.Add(darkPath);
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
                if (ManagerData.LayersData != null) {
                    ManagerData.LayersData.CollectionChanged -= LayersData_CollectionChanged;
                }

                this.Children.Clear();
            }

            // 非托管资源清理

            _isDisposed = true;
        }
        #endregion

        private InputCursor OriginalInputCursor { get; set; }
        private bool _isDisposed;
        private bool _isInitialized;
        private readonly Grid _workerground = new();
        private readonly Dictionary<CanvasLayerData, CanvasLayer> _layerMap = [];
        private ToolManager _tool;
        //private bool _isDrawable = false, _isDrawing = false;
        //private Path _currentPath; // 当前正在绘制的路径
        //private PathGeometry _pathGeometry; // 当前正在绘制的路径
        //private STADraw _currentDraw;  // 当前线条的数据模型
        private static readonly BindingInfo[] _cachedBindingInfos = [
            new BindingInfo(WidthProperty, "Size.Width", BindingMode.OneWay),
            new BindingInfo(HeightProperty, "Size.Height", BindingMode.OneWay),
        ];
    }
}
