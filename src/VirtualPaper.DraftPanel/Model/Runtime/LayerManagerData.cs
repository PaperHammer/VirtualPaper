using System.Collections.Specialized;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.DraftPanel.Model.Runtime {
    [JsonSerializable(typeof(LayerManagerData))]
    [JsonSerializable(typeof(CanvasLayerData))]
    [JsonSerializable(typeof(ObservableList<CanvasLayerData>))]
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
                if (_size == value) return;
                _size = value;
                OnPropertyChanged();
            }
        }

        public ObservableList<CanvasLayerData> LayersData { get; set; } = [];

        CanvasLayerData _selectedLayerData;
        [JsonIgnore]
        public CanvasLayerData SelectedLayerData {
            get => _selectedLayerData;
            set {
                if (value == null || value == _selectedLayerData) return;
                _selectedLayerData = value;
                OnPropertyChanged();
            }
        }
        //private int _selectedIndex = 0;
        //public int SelectedIndex {
        //    get => _selectedIndex;
        //    set {
        //        if (_selectedIndex == value || value < 0) return;
        //        _selectedIndex = value;
        //        OnPropertyChanged();
        //    }
        //}

        string _canvasSizeText;
        [JsonIgnore]
        public string CanvasSizeText {
            get { return _canvasSizeText; }
            private set { _canvasSizeText = value; OnPropertyChanged(); }
        }

        private void LayersData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.NewItems == null || e.NewItems.Count == 0) {
                SelectedLayerData = LayersData.FirstOrDefault();
            }
            else {
                SelectedLayerData = e.NewItems[0] as CanvasLayerData;
            }
        }

        public async Task SaveAsync(string filePath) {
            if (LayersData.Count == 0) {
                LayersData.Add(new());
            }
            foreach (var layer in LayersData) {
                await layer.SaveAsync(filePath);
            }
            await JsonSaver.SaveAsync(filePath, this, LayerManagerDataContext.Default);
        }

        public async Task LoadAsync(string filePath) {
            var managerData = await JsonSaver.LoadAsync<LayerManagerData>(filePath, LayerManagerDataContext.Default);
            this.Size = managerData.Size;
            this.LayersData.CollectionChanged += LayersData_CollectionChanged;
            this.LayersData.SetRange(managerData.LayersData);

            foreach (var layer in LayersData) {
                await layer.LoadAsync(filePath);
            }
        }
    }
}
