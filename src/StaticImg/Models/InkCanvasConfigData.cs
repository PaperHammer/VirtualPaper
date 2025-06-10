using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Input;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;
using Windows.Foundation;
using Windows.UI;

namespace Workloads.Creation.StaticImg.Models {
    [JsonSerializable(typeof(InkCanvasConfigData))]
    [JsonSerializable(typeof(ObservableList<InkCanvasData>))]
    [JsonSerializable(typeof(ObservableList<Color>))]
    internal partial class InkCanvasConfigDataContext : JsonSerializerContext { }

    internal partial class InkCanvasConfigData : ObservableObject {
        public event EventHandler InkDataEnabledChanged;
        public event EventHandler SeletcedToolChanged;
        public event EventHandler SeletcedLayerChanged;
        public event EventHandler<double> SelectedCropAspectClicked;
        public event EventHandler<ArcSize> SizeChanged;

        #region serilizable properties
        public ObservableList<InkCanvasData> InkDatas { get; set; } = [];
        public ObservableList<Color> CustomColors { get; set; } = [];

        ArcSize _size = new(1920, 1080, 96, RebuildMode.None); // 像素
        public ArcSize Size {
            get => _size;
            set { _size = value; ArcSizeChanged(); OnPropertyChanged(); }
        }
        #endregion

        float _canvasZoom; // 0.2 -- 7.0
        [JsonIgnore]
        public float CanvasZoom {
            get { return _canvasZoom; }
            set {
                if (Consts.IsDoubleValueEqual(_canvasZoom, value)) return;
                _canvasZoom = value;
                OnPropertyChanged();
            }
        }

        string _pointerPosText;
        [JsonIgnore]
        public string PointerPosText {
            get { return _pointerPosText; }
            set { _pointerPosText = value; OnPropertyChanged(); }
        }

        Color _foregroundColor = Colors.Black;
        [JsonIgnore]
        public Color ForegroundColor {
            get => _foregroundColor;
            set { if (_foregroundColor == value) return; _foregroundColor = value; OnPropertyChanged(); }
        }

        Color _backgroundColor = Colors.White;
        [JsonIgnore]
        public Color BackgroundColor {
            get => _backgroundColor;
            set { if (_backgroundColor == value) return; _backgroundColor = value; OnPropertyChanged(); }
        }

        double _tolerance = 100;
        [JsonIgnore]
        public double Tolerance {
            get => _tolerance;
            set { if (_tolerance == value) return; _tolerance = value; OnPropertyChanged(); }
        }

        string _canvasSizeText;
        [JsonIgnore]
        public string CanvasSizeText {
            get { return _canvasSizeText; }
            private set { _canvasSizeText = value; OnPropertyChanged(); }
        }

        private Rect _selectionRect;
        [JsonIgnore]
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

        string _selectionSizeText;
        [JsonIgnore]
        public string SelectionSizeText {
            get { return _selectionSizeText; }
            private set { _selectionSizeText = value; OnPropertyChanged(); }
        }

        private InkCanvasData _selectedInkCanvas;
        [JsonIgnore]
        public InkCanvasData SelectedInkCanvas {
            get { return _selectedInkCanvas; }
            set {
                if (value == null || _selectedInkCanvas == value) return;
                _selectedInkCanvas = value;
                SeletcedLayerChanged?.Invoke(this, EventArgs.Empty);
                OnPropertyChanged();

                if (value.IsEnable)
                    MainPage.Instance.Bridge.GetNotify().CloseAndRemoveMsg(nameof(Constants.I18n.Draft_SI_LayerLocked));                
            }
        }

        PaintBrushItem _seletcedBrush;
        [JsonIgnore]
        public PaintBrushItem SelectedBrush {
            get { return _seletcedBrush; }
            set { if (_seletcedBrush == value) return; _seletcedBrush = value; OnPropertyChanged(); }
        }

        AspectRatioItem _seletcedAspectItem;
        [JsonIgnore]
        public AspectRatioItem SeletcedAspectItem {
            get { return _seletcedAspectItem; }
            set {
                if (_seletcedAspectItem == value) return;
                _seletcedAspectItem = value; SelectedCropAspectClicked?.Invoke(this, value.Ratio); OnPropertyChanged();
            }
        }

        ToolItem _selectedToolItem;
        [JsonIgnore]
        public ToolItem SelectedToolItem {
            get { return _selectedToolItem; }
            set {
                if (_selectedToolItem == value || value == null) return;
                _selectedToolItem = value; SeletcedToolChanged?.Invoke(this, EventArgs.Empty); OnPropertyChanged();
            }
        }

        double _brushThickness = 5;
        [JsonIgnore]
        public double BrushThickness {
            get { return _brushThickness; }
            set { if (_brushThickness == value) return; _brushThickness = value; OnPropertyChanged(); }
        }

        double _brushOpacity = 100;
        [JsonIgnore]
        public double BrushOpacity {
            get { return _brushOpacity; }
            set { if (_brushOpacity == value) return; _brushOpacity = value; OnPropertyChanged(); }
        }

        double _eraserSize = 10;
        [JsonIgnore]
        public double EraserSize {
            get => _eraserSize;
            set { if (_eraserSize == value) return; _eraserSize = value; OnPropertyChanged(); }
        }

        double _eraserOpacity = 100;
        [JsonIgnore]
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
        [JsonIgnore]
        public int EraserFeather { get; internal set; } = 0;

        [JsonConstructor]
        [Obsolete("This constructor is intended for JSON deserialization only. Use the another method instead.")]
        internal InkCanvasConfigData() { }

        public InkCanvasConfigData(string entryFilePath) {
            _entryFilePath = entryFilePath;
        }

        private void OnInkDataChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(InkCanvasData.IsEnable)) {
                InkDataEnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        internal async Task InitDataAsync() {
            await AddLayerAsync("背景", true);
            await AddLayerAsync();
        }

        internal async Task SaveBasicAsync() {
            await JsonSaver.SaveAsync(_entryFilePath, this, InkCanvasConfigDataContext.Default);
        }

        internal async Task SaveRenderDataAsync() {
            //foreach (var ink in InkDatas) {
            //    await ink.SaveAsync();
            //}
            await Task.WhenAll(InkDatas.Select(ink => ink.SaveAsync()));
        }

        internal async Task LoadRenderDataAsync() {
            await _isInkDataLoadCompleted.Task;
            //foreach (var ink in InkDatas) {
            //    ink.RenderData = new(Size, ink.IsRootBackground);
            //    await ink.LoadAsync();
            //}
            var loadTasks = InkDatas.Select(async ink => {
                ink.RenderData = new(Size, ink.IsRootBackground);
                await ink.LoadAsync();
            });

            await Task.WhenAll(loadTasks);
        }

        internal async Task LoadBasicDataAsync() {
            var tmp = await JsonSaver.LoadAsync<InkCanvasConfigData>(_entryFilePath, InkCanvasConfigDataContext.Default);
            this.Size = tmp.Size;
            this.InkDatas.SetRange(tmp.InkDatas);
            _isInkDataLoadCompleted.TrySetResult(true);
            this.CustomColors.SetRange(tmp.CustomColors);

            DiscretizeZIndexesOnLoad(tmp.InkDatas);
        }

        private void DiscretizeZIndexesOnLoad(IEnumerable<InkCanvasData> data) {
            var sortedLayers = data
                .OrderBy(layer => layer.ZIndex)
                .ToList();

            for (int i = 0; i < sortedLayers.Count; i++) {
                sortedLayers[i].SetDataFilePath(_entryFilePath);
                sortedLayers[i].ZIndex = i;
            }
        }

        public async Task<InkCanvasData> AddLayerAsync(string name = null, bool isBackground = false) {
            InkCanvasData layerData = new(_entryFilePath, isBackground) {
                Name = name ?? $"图层_{_nextLayerNumberTag++}",
                ZIndex = InkDatas.Count,
                RenderData = new(Size, isBackground),
            };
            await AddAsync(layerData);

            return layerData;
        }

        private async Task AddAsync(InkCanvasData data) {
            this.InkDatas.Insert(0, data);
            await SaveBasicAsync();
            await data.LoadAsync();
        }

        internal async Task<InkCanvasData> CopyLayerAsync(long itemTag) {
            var idx = InkDatas.FindIndex(x => x.Tag == itemTag);
            if (idx < 0) return null;

            var newLayer = InkDatas[idx].Clone();
            newLayer.Name += $"_副本{_nextCopyedLayerNumberTag++}";
            newLayer.ZIndex = InkDatas.Count;
            await AddAsync(newLayer);

            return newLayer;
        }

        internal async Task RenameAsync(long itemTag) {
            var idx = InkDatas.FindIndex(x => x.Tag == itemTag);
            if (idx < 0) return;

            string oldName = InkDatas[idx].Name;
            var viewModel = new RenameViewModel(oldName);
            var dialogRes = await MainPage.Instance.Bridge.GetDialog().ShowDialogAsync(
                new RenameView(viewModel),
                LanguageUtil.GetI18n(nameof(Constants.I18n.Dialog_Title_Rename)),
                LanguageUtil.GetI18n(Constants.I18n.Text_Confirm),
                LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
            if (dialogRes != DialogResult.Primary || !ComplianceUtil.IsValidValueOnlyLength(viewModel.NewName)) return;
            InkDatas[idx].Name = viewModel.NewName;

            await SaveBasicAsync();
        }

        internal async Task DeleteAsync(long itemTag) {
            var idx = InkDatas.FindIndex(x => x.Tag == itemTag);
            if (idx < 0) return;

            if (InkDatas[idx].IsRootBackground) {
                MainPage.Instance.Bridge.GetNotify().ShowWarn(nameof(Constants.I18n.Project_CannotDelete_RootBackground));
                return;
            }

            await InkDatas[idx].DeletAsync();
            InkDatas.RemoveAt(idx);
            await SaveBasicAsync();
        }

        internal async Task UpdateCustomColorsAsync(ColorChangeEventArgs e) {
            if (e.OldItem != null)
                CustomColors.Remove((Color)e.OldItem);
            if (e.NewItem != null)
                CustomColors.Add((Color)e.NewItem);
            await SaveBasicAsync();
        }

        internal void UpdateForegroundColor(ColorChangeEventArgs e) {
            if (e.NewItem != null)
                ForegroundColor = (Color)e.NewItem;
            //await SaveBasicAsync();
        }

        internal void UpdateBackgroundColor(ColorChangeEventArgs e) {
            if (e.NewItem != null)
                BackgroundColor = (Color)e.NewItem;
            //await SaveBasicAsync();
        }

        internal void UpdatePointerPos(Point? position = null) {
            PointerPosText = position == null || !IsPointerOverTaregt(position) ?
                string.Empty :
                $"{position.Value.X:F0}, {position.Value.Y:F0} px";
        }

        private bool IsPointerOverTaregt(Point? position) {
            return position.Value.X >= 0 && position.Value.X < Size.Width &&
                   position.Value.Y >= 0 && position.Value.Y < Size.Height;
        }

        private void UpdateSelectionSizeText() {
            SelectionSizeText = _selectionRect.IsEmpty ?
                string.Empty :
                $"W: {_selectionRect.Width:F0} px, H: {_selectionRect.Height:F0} px";
        }

        private async void ArcSizeChanged() {
            var tasks = InkDatas
                .Where(ink => ink.RenderData != null)
                .Select(ink => ink.RenderData.ResizeRenderTargetAsync(Size))
                .ToList();
            await Task.WhenAll(tasks);
            CanvasSizeText = $"{Size.Width:F0} * {Size.Height:F0} px ({Size.Dpi} / {ArcSize.HardwareDpi} DPI)";
            SizeChanged?.Invoke(this, Size);
        }

        private int _nextLayerNumberTag = 1;
        private int _nextCopyedLayerNumberTag = 1;
        private readonly string _entryFilePath;
        private readonly TaskCompletionSource<bool> _isInkDataLoadCompleted = new();
    }
}
