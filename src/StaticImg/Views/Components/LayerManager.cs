using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
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
using Workloads.Creation.StaticImg.Models.Events;
using Workloads.Creation.StaticImg.Models.ToolItemUtil;
using Workloads.Creation.StaticImg.Utils;

namespace Workloads.Creation.StaticImg.Views.Components {
    internal partial class LayerManager : Grid, IDisposable { // ui
        public LayerManagerData ManagerData {
            get { return (LayerManagerData)GetValue(ManagerDataProperty); }
            set { SetValue(ManagerDataProperty, value); }
        }
        public static readonly DependencyProperty ManagerDataProperty =
            DependencyProperty.Register(
                nameof(ManagerData),
                typeof(LayerManagerData),
                typeof(LayerManager),
                new PropertyMetadata(null, OnManagerDataChanged));

        private Point? _ponterPos;
        public Point? PointerPos {
            get { return _ponterPos; }
            set {
                if (_ponterPos == value) return;

                if (value == null) {
                    _ponterPos = null;
                    ManagerData.PointerPosText = string.Empty;
                    return;
                }

                _ponterPos = value;
                ArcPoint formatPos = ArcPoint.FormatPoint(value.Value, 0);
                ManagerData.PointerPosText = value == null ? string.Empty : $"{formatPos.X}, {formatPos.Y} {"像素"}";
            }
        }

        #region init
        public LayerManager() {
            InitProperty();

            this.Loading += LayerManager_Loading;
            this.Loaded += LayerManager_Loaded;
            this.PointerEntered += LayerManager_PointerEntered;
            this.PointerPressed += LayerManager_PointerPressed;
            this.PointerMoved += LayerManager_PointerMoved;
            this.PointerReleased += LayerManager_PointerReleased;
            this.PointerExited += LayerManager_PointerExited;
        }

        private void LayerManager_Loading(FrameworkElement sender, object args) {
            InitDataContext();
        }

        private void LayerManager_Loaded(object sender, RoutedEventArgs e) {
            RenderWorkerground();
        }

        private void InitDataContext() {
            this.DataContext = ManagerData;
            foreach (var bindingInfo in _layerManagerBindingInfos) {
                BindingsUtil.ApplyBindings(this, bindingInfo);
            }
        }

        private void InitProperty() {
            this.Margin = new Thickness(80);
            this.Background = new SolidColorBrush(Colors.Transparent);
            this.BorderThickness = new Thickness(1);
            this.BorderBrush = new SolidColorBrush(Colors.Gray);
        }

        private void RenderWorkerground() {
            _workerground.Width = this.Width;
            _workerground.Height = this.Height;
            Canvas.SetZIndex(_workerground, -1);
            this.Children.Add(_workerground);
            DrawGridWorkerground();
        }

        private void DrawGridWorkerground() {
            // 网格间距
            int gridSize = 10;
            var color1 = Colors.LightGray; // 浅灰色
            var color2 = Colors.DarkGray;  // 深灰色

            // 分别存储浅色和深色方块
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

            var lightPath = new Path {
                Fill = new SolidColorBrush(color1),
                Data = lightGeometryGroup,
            };
            var darkPath = new Path {
                Fill = new SolidColorBrush(color2),
                Data = darkGeometryGroup,
            };

            _workerground.Children.Add(lightPath);
            _workerground.Children.Add(darkPath);
        }
        #endregion

        #region data
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
                control._tool.RegisterTool(ToolType.Eraser, new EraserTool(managerData));
            }
        }

        private void LayersData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            UpdateLayer(e);
        }

        private void UpdateLayer(NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    foreach (var layerData in e.NewItems)
                        AddElement(layerData as CanvasLayerData);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var layerData in e.OldItems)
                        RemoveElement(layerData as CanvasLayerData);
                    break;
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Reset:
                default:
                    break;
            }
        }

        public void AddElement(CanvasLayerData layerData) {
            if (_layerMap.TryGetValue(layerData, out var weakRef) && weakRef.TryGetResources(out _)) {
                return;
            }
            var canvasControl = new CanvasControl() {
                DataContext = layerData,
            };
            this.Children.Add(canvasControl);
            canvasControl.Loaded += (s, e) => {
                var resources = new CanvasLayerResources(canvasControl, _sharedDevice, ManagerData);
                var reference = new CanvasLayerReference(resources, () => {
                    _layerMap.Remove(layerData);
                });

                _layerMap.Add(layerData, reference);
                canvasControl.SizeChanged += (s, e) => {
                    if (reference.TryGetResources(out var res))
                        res.ResizeRenderTarget();
                };
            };

            InitDataContext(canvasControl, _canvasControlBindingInfos);
            // 注册事件（通过弱事件模式避免内存泄漏）
            RegisterPointerEvents(canvasControl);
        }

        public bool TryGetResources(CanvasLayerData layerData, out CanvasLayerResources resources) {
            resources = null;
            return _layerMap.TryGetValue(layerData, out var reference) &&
                   reference.TryGetResources(out resources);
        }

        private void RemoveElement(CanvasLayerData layerData) {
            if (_layerMap.TryGetValue(layerData, out var weakRef)) {
                if (weakRef.TryGetResources(out var recource)) {
                    this.Children.Remove(recource.Control);
                }
                _layerMap.Remove(layerData);
            }
        }

        private static void InitDataContext(FrameworkElement frameworkElement, BindingInfo[] bindingInfos) {
            foreach (var bindingInfo in bindingInfos) {
                BindingsUtil.ApplyBindings(frameworkElement, bindingInfo);
            }
        }

        private void RegisterPointerEvents(CanvasControl canvas) {
            var weakHandler = new WeakPointerEventHandler(this);

            canvas.Draw += weakHandler.OnDraw;
        }

        private void CleanupInvalidReferences() {
            var deadKeys = _layerMap
                .Where(kvp => !kvp.Value.TryGetResources(out _))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in deadKeys) {
                _layerMap.Remove(key);
            }
        }

        public CanvasLayerResources GetResources(CanvasLayerData layerData) {
            if (_layerMap.TryGetValue(layerData, out var weakRef)) {
                weakRef.TryGetResources(out var resources);
                return resources;
            }
            return null;
        }
        #endregion

        #region uielement event
        private void LayerManager_PointerExited(object sender, PointerRoutedEventArgs e) {
            PointerPos = null;
            HandleToolEvent(tool => tool.OnPointerExited(new(e, GetResources(ManagerData.SelectedLayerData))));

            if (OriginalInputCursor != null) {
                this.ProtectedCursor = OriginalInputCursor;
            }
        }

        private void LayerManager_PointerReleased(object sender, PointerRoutedEventArgs e) {
            HandleToolEvent(tool => tool.OnPointerReleased(new(e, GetResources(ManagerData.SelectedLayerData))));
        }

        private void LayerManager_PointerMoved(object sender, PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(this);
            PointerPos = pointerPoint.Position;

            HandleToolEvent(tool => tool.OnPointerMoved(new(e, GetResources(ManagerData.SelectedLayerData))));
        }

        private void LayerManager_PointerPressed(object sender, PointerRoutedEventArgs e) {
            HandleToolEvent(tool => tool.OnPointerPressed(new(e, GetResources(ManagerData.SelectedLayerData))));
        }

        private void LayerManager_PointerEntered(object sender, PointerRoutedEventArgs e) {
            OriginalInputCursor = this.ProtectedCursor ?? InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            this.ProtectedCursor = ManagerData.SelectedToolItem?.Cursor;

            if (ManagerData.SelectedLayerData == null) {
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Draft_SI_LayerNotAvailable), InfoBarType.Error, key: nameof(Constants.I18n.Draft_SI_LayerNotAvailable), isAllowDuplication: false);
                return;
            }

            if (!ManagerData.SelectedLayerData.IsEnable) {
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Draft_SI_LayerLocked), InfoBarType.Warning, key: nameof(Constants.I18n.Draft_SI_LayerLocked), isAllowDuplication: false);
                return;
            }

            HandleToolEvent(tool => tool.OnPointerEntered(new(e, GetResources(ManagerData.SelectedLayerData))));
        }

        internal void OnDraw(CanvasControl sender, CanvasDrawEventArgs args) {
            HandleToolEvent(tool => tool.OnDraw(sender, args));
        }

        private async void HandleToolEvent(Action<Tool> action) {
            var selectedTool = _tool.GetTool(ManagerData.SelectedToolItem.Type);
            if (selectedTool == null) {
                return;
            }

            var resources = GetResources(ManagerData.SelectedLayerData);
            if (resources == null) return;

            if (resources.Control != null && !resources.Control.IsLoaded) {
                await resources.IsCompleted.Task.ConfigureAwait(false);
            }

            if (selectedTool != null) {
                action(selectedTool);
            }
        }
        #endregion

        #region diapose
        private bool _isDisposed;
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
            _isDisposed = true;
        }
        #endregion

        private InputCursor OriginalInputCursor { get; set; }
        private readonly Grid _workerground = new();
        private readonly Dictionary<CanvasLayerData, CanvasLayerReference> _layerMap = [];
        private readonly CanvasDevice _sharedDevice = CanvasDevice.GetSharedDevice();
        private ToolManager _tool;
        private static readonly BindingInfo[] _layerManagerBindingInfos = [
            new BindingInfo(WidthProperty, "Size.Width", BindingMode.OneWay),
            new BindingInfo(HeightProperty, "Size.Height", BindingMode.OneWay),
        ];
        private static readonly BindingInfo[] _canvasControlBindingInfos = [
            new BindingInfo(CanvasControl.OpacityProperty, "Opacity", BindingMode.OneWay),
            new BindingInfo(CanvasControl.VisibilityProperty, "IsEnable", BindingMode.OneWay, new BooleanToVisibilityConverter())
        ];
    }
}
