using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Input;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Models.ToolItems;

namespace Workloads.Creation.StaticImg.Models.Specific {
    //  UIContext part of InkCanvasData
    public partial class InkCanvasData : ObservableObject {
        public event EventHandler? SeletcedToolChanged;
        public event EventHandler<double>? SelectedCropAspectClicked;
        public event EventHandler<LayerSizeChangedEventArgs>? SizeChanged;

        public ObservableCollection<Color> CustomColors { get; set; } = [];

        // 0 表示未锁定，1 表示正在处理中
        private int _canvasSizeLock = 0;
        ArcSize _canvasSize; // 像素
        public ArcSize CanvasSize {
            get => _canvasSize;
            set {
                // 避免反转、缩放在 undo/redo 下失效
                bool isForceUpdate = value.Rebuild != RebuildMode.RotateRight || value.Rebuild != RebuildMode.RotateLeft;
                if ((!isForceUpdate && Equals(_canvasSize, value)) || Interlocked.CompareExchange(ref _canvasSizeLock, 1, 0) != 0) {
                    return;
                }

                try {
                    _canvasSize = value;
                    UpdateCanvasSizeText();
                    OnPropertyChanged();
                    _ = ApplyCanvasSizeChangeAsync(value);
                }
                catch (Exception ex) {
                    GlobalMessageUtil.ShowError(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex.Message);
                    ArcLog.GetLogger<CropTool>().Error(ex);
                }
                finally {
                    Interlocked.Exchange(ref _canvasSizeLock, 0);
                }
            }
        }

        double _canvasZoom; // 0.2 -- 7.0
        public double CanvasZoom {
            get { return _canvasZoom; }
            set {
                var normalizedValue = Math.Round(value, 3);
                if (Consts.IsDoubleValueEqual(_canvasZoom, normalizedValue)) return;
                _canvasZoom = normalizedValue;
                OnPropertyChanged();
            }
        }

        string _pointerPosText = string.Empty;
        public string PointerPosText {
            get { return _pointerPosText; }
            set { _pointerPosText = value; OnPropertyChanged(); }
        }

        Color _foregroundColor = Colors.Black;
        public Color ForegroundColor {
            get => _foregroundColor;
            set { if (_foregroundColor == value) return; _foregroundColor = value; OnPropertyChanged(); }
        }

        Color _backgroundColor = Colors.White;
        public Color BackgroundColor {
            get => _backgroundColor;
            set { if (_backgroundColor == value) return; _backgroundColor = value; OnPropertyChanged(); }
        }

        string _canvasSizeText = string.Empty;
        public string CanvasSizeText {
            get { return _canvasSizeText; }
            private set { _canvasSizeText = value; OnPropertyChanged(); }
        }

        private Rect _selectionRect;
        public Rect SelectionRect {
            get => _selectionRect;
            set {
                if (_selectionRect != value) {
                    _selectionRect = value;
                    OnPropertyChanged();
                    UpdateSelectionSizeText();
                }
            }
        }

        string _selectionSizeText = string.Empty;
        public string SelectionSizeText {
            get { return _selectionSizeText; }
            private set { _selectionSizeText = value; OnPropertyChanged(); }
        }

        PaintBrushItem _seletcedBrush;
        public PaintBrushItem SelectedBrush {
            get { return _seletcedBrush; }
            set { if (_seletcedBrush == value) return; _seletcedBrush = value; OnPropertyChanged(); }
        }

        AspectRatioItem? _seletcedAspectItem;
        public AspectRatioItem? SeletcedAspectItem {
            get { return _seletcedAspectItem; }
            set {
                if (_seletcedAspectItem == value) return;
                _seletcedAspectItem = value; SelectedCropAspectClicked?.Invoke(this, value == null ? -1 : value.Ratio); OnPropertyChanged();
            }
        }

        ToolItem _selectedToolItem;
        public ToolItem SelectedToolItem {
            get { return _selectedToolItem; }
            set {
                if (_selectedToolItem == value || value == null) return;
                _selectedToolItem = value; SeletcedToolChanged?.Invoke(this, EventArgs.Empty); OnPropertyChanged();
            }
        }

        double _tolerance = 100;
        public double Tolerance {
            get => _tolerance;
            set { if (_tolerance == value) return; _tolerance = value; OnPropertyChanged(); }
        }

        double _brushThickness = 5;
        public double BrushThickness {
            get { return _brushThickness; }
            set { if (_brushThickness == value) return; _brushThickness = value; OnPropertyChanged(); }
        }

        double _brushOpacity = 100;
        public double BrushOpacity {
            get { return _brushOpacity; }
            set { if (_brushOpacity == value) return; _brushOpacity = value; OnPropertyChanged(); }
        }

        double _eraserSize = 10;
        public double EraserSize {
            get => _eraserSize;
            set { if (_eraserSize == value) return; _eraserSize = value; OnPropertyChanged(); }
        }

        double _eraserOpacity = 100;
        public double EraserOpacity {
            get { return _eraserOpacity; }
            set { if (_eraserOpacity == value) return; _eraserOpacity = value; OnPropertyChanged(); }
        }

        // 控制擦除边缘羽化效果 为擦除区域添加平滑过渡的边缘效果
        /*
         * 值范围	效果	视觉表现
            = 0	硬边缘擦除	擦除边界锐利清晰
            0 < value < 5	轻微羽化	边缘半透明过渡
            >= 5	强羽化	边缘模糊渐变
         */
        public int EraserFeather { get; internal set; } = 0;

        internal void InitData() {
            UpdateCanvasSizeText();
            AddLayer(LanguageUtil.GetI18n(nameof(Constants.I18n.Project_SI_Text_BackgroundLayer)), true);
        }

        private void UpdateCanvasSizeText() {
            if (CanvasSize != default) {
                var rasterizationScale = WindowConsts.ArcWindowInstance?.Content?.XamlRoot?.RasterizationScale ?? 1.0;
                CanvasSizeText = $"{CanvasSize.Width:F0} * {CanvasSize.Height:F0} px ({CanvasSize.Dpi} / {rasterizationScale * 96} DPI)";
            }
        }

        internal async Task UpdateCustomColorsAsync(ColorChangeEventArgs e) {
            if (e.OldItem != null) CustomColors.Remove((Color)e.OldItem);
            if (e.NewItem != null) CustomColors.Add((Color)e.NewItem);

            var curBusdata = _session.DesignFileUtil.BusinessDataCache.Clone();
            curBusdata.SetColors(CustomColors);
            await _session.DesignFileUtil.SaveBusinessDataAsync(curBusdata);
        }

        internal void UpdateForegroundColor(ColorChangeEventArgs e) {
            if (e.NewItem != null) ForegroundColor = (Color)e.NewItem;
        }

        internal void UpdateBackgroundColor(ColorChangeEventArgs e) {
            if (e.NewItem != null) BackgroundColor = (Color)e.NewItem;
        }

        internal void UpdatePointerPos(Point? position = null) {
            PointerPosText = position == null || !IsPointerOverTaregt(position) ?
                string.Empty : $"{position.Value.X:F0}, {position.Value.Y:F0} px";
        }

        private bool IsPointerOverTaregt(Point? position) {
            return position != null &&
                position.Value.X >= 0 && position.Value.X < CanvasSize.Width &&
                position.Value.Y >= 0 && position.Value.Y < CanvasSize.Height;
        }

        private void UpdateSelectionSizeText() {
            SelectionSizeText = _selectionRect.IsEmpty ?
                string.Empty : $"W: {_selectionRect.Width:F0} px, H: {_selectionRect.Height:F0} px";
        }

        private async Task ApplyCanvasSizeChangeAsync(ArcSize targetSize) {
            var tasks = _allLayers
                .Where(ink => ink.RenderData != null)
                .Select(ink => ink.RenderData.ResizeRenderTargetAsync(targetSize));
            await Task.WhenAll(tasks);
            SizeChanged?.Invoke(this, new LayerSizeChangedEventArgs(targetSize));
        }
    }
}
