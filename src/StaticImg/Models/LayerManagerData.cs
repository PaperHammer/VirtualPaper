using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.ArcEventArgs;
using VirtualPaper.UIComponent.ViewModels;
using Windows.UI;

namespace Workloads.Creation.StaticImg.Models {
    [JsonSerializable(typeof(LayerManagerData))]
    [JsonSerializable(typeof(CanvasLayerData))]
    [JsonSerializable(typeof(ObservableList<CanvasLayerData>))]
    [JsonSerializable(typeof(ObservableList<Color>))]
    internal partial class LayerManagerDataContext : JsonSerializerContext { }

    internal partial class LayerManagerData : ObservableObject {
        string _pointerPosText;
        [JsonIgnore]
        public string PointerPosText {
            get { return _pointerPosText; }
            set { _pointerPosText = value; OnPropertyChanged(); }
        }

        SizeF _size = new(1920, 1080, 96); // 像素
        public SizeF Size {
            get => _size;
            set {
                CanvasSizeText = $"{value.Width}, {value.Height} {"像素"} ({value.Dpi} / {value.HardwareDpi} DPI)";
                if (_size.Equals(value)) return;
                _size = value;
                OnPropertyChanged();
            }
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

        public ObservableList<Color> CustomColors { get; set; } = [];
        public ObservableList<CanvasLayerData> LayersData { get; set; } = [];

        CanvasLayerData _selectedLayerData;
        [JsonIgnore]
        public CanvasLayerData SelectedLayerData {
            get => _selectedLayerData;
            set {
                if (value == null || _selectedLayerData == value) return;

                _selectedLayerData = value;
                if (value.IsEnable)
                    MainPage.Instance.Bridge.GetNotify().CloseAndRemoveMsg(nameof(Constants.I18n.Draft_SI_LayerLocked));
                OnPropertyChanged();
            }
        }

        string _canvasSizeText;
        [JsonIgnore]
        public string CanvasSizeText {
            get { return _canvasSizeText; }
            private set { _canvasSizeText = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public double BrushThickness { get; internal set; } = 5;
        [JsonIgnore]
        public double BrushOpacity { get; internal set; } = 100;
        [JsonIgnore]
        public InputSystemCursor Cursor { get; internal set; } = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        [JsonIgnore]
        public ToolType SelectedToolType { get; set; }

        [JsonConstructor]
        [Obsolete("This constructor is intended for JSON deserialization only. Use the another method instead.")]
        public LayerManagerData() { }

        public LayerManagerData(string filePath) {
            _filePath = filePath;
        }

        private void LayersData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.NewItems == null || e.NewItems.Count == 0) {
                SelectedLayerData = LayersData.FirstOrDefault();
            }
            else {
                SelectedLayerData ??= e.NewItems[0] as CanvasLayerData;
            }
        }

        #region save and load
        public async Task SaveAsync() {
            foreach (var layer in LayersData) {
                await layer.SaveAsync();
            }
            await SaveLayerMangerDataAsync();
        }

        private async Task SaveLayerMangerDataAsync() {
            await JsonSaver.SaveAsync(_filePath, this, LayerManagerDataContext.Default);
        }

        public async Task LoadAsync() {
            var managerData = await JsonSaver.LoadAsync<LayerManagerData>(_filePath, LayerManagerDataContext.Default);
            this.Size = managerData.Size;
            this.ForegroundColor = managerData.ForegroundColor;
            this.BackgroundColor = managerData.BackgroundColor;
            this.LayersData.CollectionChanged += LayersData_CollectionChanged;
            this.LayersData.SetRange(managerData.LayersData);
            this.CustomColors.SetRange(managerData.CustomColors);

            DiscretizeZIndexesOnLoad(managerData.LayersData);
            // 保存原始可见性状态
            var originalVisibility = LayersData.ToDictionary(layer => layer, layer => layer.IsEnable);

            // 临时将所有图层设置为可见(生成缩略图需要保证控件可见)
            foreach (var layer in LayersData) {
                layer.IsEnable = true;
            }

            var loadTasks = LayersData.Select(layer => layer.LoadAsync());
            await Task.WhenAll(loadTasks); // 等待所有图层加载完成

            // 恢复原始可见性状态
            foreach (var kvp in originalVisibility) {
                kvp.Key.IsEnable = kvp.Value;
            }
        }
        #endregion

        private void DiscretizeZIndexesOnLoad(IEnumerable<CanvasLayerData> layersData) {
            var sortedLayers = layersData
                .OrderBy(layer => layer.ZIndex)
                .ToList();

            for (int i = 0; i < sortedLayers.Count; i++) {
                sortedLayers[i].SetFilePath(_filePath);
                sortedLayers[i].ZIndex = i;
            }
        }

        #region ui to data
        internal async Task UpdateCustomColorsAsync(ColorChnageEventArgs e) {
            if (e.OldItem != null)
                CustomColors.Remove((Color)e.OldItem);
            if (e.NewItem != null)
                CustomColors.Add((Color)e.NewItem);
            await SaveLayerMangerDataAsync();
        }

        internal async Task UpdateForegroundColorsAsync(ColorChnageEventArgs e) {
            if (e.NewItem != null)
                ForegroundColor = (Color)e.NewItem;
            await SaveLayerMangerDataAsync();
        }

        internal async Task UpdateBackgroundColorsAsync(ColorChnageEventArgs e) {
            if (e.NewItem != null)
                BackgroundColor = (Color)e.NewItem;
            await SaveLayerMangerDataAsync();
        }
        #endregion

        #region layer operation       
        public async Task InitDataAsync() {
            await AddLayerAsync("背景", UintColor.White, true);
            await AddLayerAsync();
        }

        public async Task AddLayerAsync() {
            CanvasLayerData layerData = new(_filePath, Size.Width, Size.Height) {
                Name = $"图层_{_nextLayerNumberTag++}",
                ZIndex = LayersData.Count,
            };
            await AddAsync(layerData);
        }

        public async Task AddLayerAsync(string name, uint background, bool isBackground = false) {
            CanvasLayerData layerData = new(_filePath, Size.Width, Size.Height, isBackground) {
                Name = name,
                //Background = background,
                ZIndex = LayersData.Count,
            };
            await AddAsync(layerData);
        }

        private async Task AddAsync(CanvasLayerData layerData) {
            this.LayersData.Insert(0, layerData);
            await layerData.SaveAsync();
            await SaveLayerMangerDataAsync();
        }

        internal async Task CopyLayerAsync(long itemTag) {
            var idx = LayersData.FindIndex(x => x.Tag == itemTag);
            if (idx < 0) return;

            CanvasLayerData copyedData = await LayersData[idx].CopyAsync();
            copyedData.Name = LayersData[idx].Name + $"-副本{_nextCopyedLayerNumberTag++}";
            copyedData.ZIndex = LayersData.Count;
            await AddAsync(copyedData);
            await copyedData.LoadAsync();
        }

        internal async Task RenameAsync(long itemTag) {
            var idx = LayersData.FindIndex(x => x.Tag == itemTag);
            if (idx < 0) return;

            string oldName = LayersData[idx].Name;
            var viewModel = new RenameViewModel(oldName);
            var dialogRes = await MainPage.Instance.Bridge.GetDialog().ShowDialogAsync(
                new RenameView(viewModel),
                LanguageUtil.GetI18n(nameof(Constants.I18n.Dialog_Title_Rename)),
                LanguageUtil.GetI18n(Constants.I18n.Text_Confirm),
                LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
            if (dialogRes != DialogResult.Primary || !ComplianceUtil.IsValidValueOnlyLength(viewModel.NewName)) return;
            LayersData[idx].Name = viewModel.NewName;

            await this.SaveAsync();
        }

        internal async Task DeleteAsync(long itemTag) {
            var idx = LayersData.FindIndex(x => x.Tag == itemTag);
            if (idx < 0) return;

            if (LayersData[idx].IsRootBackground) {
                MainPage.Instance.Bridge.GetNotify().ShowWarn(nameof(Constants.I18n.Project_CannotDelete_RootBackground));
                return;
            }

            await LayersData[idx].DeletAsync();
            LayersData.RemoveAt(idx);
            await SaveLayerMangerDataAsync();
        }
        #endregion

        private int _nextLayerNumberTag = 1;
        private int _nextCopyedLayerNumberTag = 1;
        private readonly string _filePath;
    }
}
