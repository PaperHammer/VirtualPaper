using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model.EventArg;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.DraftPanel.Model.Runtime {
    [JsonSerializable(typeof(CanvasLayerData))]
    internal partial class CanvasLayerDataContext : JsonSerializerContext { }

    internal partial class CanvasLayerData : ObservableObject {
        public event EventHandler OnDataLoaded;
        public event EventHandler<PolylineEventArgs> OnDrawsChanging;
        public event EventHandler OnDrawsChanged;

        public TaskCompletionSource<bool> RenderCompleted => _renderCompleted;

        private string _name = string.Empty;
        public string Name {
            get => _name == string.Empty ? _name = $"图层 {_nextAvailable++}" : _name;
            set { if (_name == value) return; _name = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public long Tag => IdentifyUtil.GenerateIdShort();

        uint _background = 4294967295;
        public uint Background {
            get => _background;
            set { if (_background == value) return; _background = value; OnPropertyChanged(); }
        }

        float _opacity = 1f;
        public float Opacity {
            get => _opacity;
            set { if (_opacity == value) return; _opacity = value; OnPropertyChanged(); }
        }

        bool _isEnable = true;
        public bool IsEnable {
            get => _isEnable;
            set {
                if (_isEnable == value) return;
                _isEnable = value;
                if (value)
                    Draft.Instance.GetNotify().CloseAndRemoveMsg(nameof(Constants.I18n.Draft_SI_LayerLocked));
                OnPropertyChanged();
            }
        }

        ImageSource _layerThum;
        [JsonIgnore]
        public ImageSource LayerThum {
            get { return _layerThum; }
            set { _layerThum = value; OnPropertyChanged(); }
        }

        [Key(1)]
        [JsonIgnore]
        public ObservableCollection<STAImage> Images { get; private set; } = []; // 包含的所有外部图像
        [Key(2)]
        [JsonIgnore]
        public ObservableCollection<STADraw> Draws { get; private set; } = []; // 包含的所有绘制线条

        public CanvasLayerData Copy() {
            CanvasLayerData newLayerData = new() {
                Name = this.Name + $"-副本{_nextCopyAvailable++}",
                Background = this.Background,
                Opacity = this.Opacity,
                IsEnable = this.IsEnable,
                Images = [.. this.Images],
                Draws = [.. this.Draws],
            };

            return newLayerData;
        }

        public async Task SaveAsync(string entryFilePath) {
            await _saveQueueLock.WaitAsync();
            try {
                string filePathForDarws = entryFilePath + ".draws";
                string filePathForImages = entryFilePath + ".images";

                //await _drawSaver.SaveToBufferAsync(Draws, filePathForDarws);
                //await _drawSaver.SaveManuallyAsync();
                //await _imageSaver.SaveToBufferAsync(Images, filePathForImages);
                //await _imageSaver.SaveManuallyAsync();

                await MessagePackSaver.SaveAsync(filePathForDarws, Draws);
                await MessagePackSaver.SaveAsync(filePathForImages, Images);
            }
            catch (Exception ex) {
                Draft.Instance.GetNotify().ShowExp(ex);
            }
            finally {
                _saveQueueLock.Release();
            }
        }

        public async Task LoadAsync(string entryFilePath) {
            try {
                this.Draws = await MessagePackSaver.LoadAsync<ObservableCollection<STADraw>>(entryFilePath + ".draws") ?? [];
                this.Images = await MessagePackSaver.LoadAsync<ObservableCollection<STAImage>>(entryFilePath + ".images") ?? [];
                OnDataLoaded?.Invoke(this, EventArgs.Empty);

                await RenderCompleted.Task;
            }
            catch (Exception ex) {
                Draft.Instance.GetNotify().ShowExp(ex);
            }
        }

        internal void AddDraw(Polyline currentLine, STADraw currentDraw) {
            Draws.Add(currentDraw);
            OnDrawsChanging?.Invoke(this, new(currentLine, OperationType.Add));
        }

        internal void RemoveDraw(Polyline currentLine, STADraw currentDraw) {
            Draws.Remove(currentDraw);
            OnDrawsChanging?.Invoke(this, new(currentLine, OperationType.Remove));
        }

        internal void DrawsChanged() {
            OnDrawsChanged?.Invoke(this, EventArgs.Empty);
        }

        private int _nextAvailable = 1;
        private int _nextCopyAvailable = 1;
        private readonly SemaphoreSlim _saveQueueLock = new(1, 1);
        private readonly TaskCompletionSource<bool> _renderCompleted = new();
        //private readonly BufferSaver<ObservableCollection<STAImage>> _imageSaver = new();
        //private readonly BufferSaver<ObservableCollection<STADraw>> _drawSaver = new();
    }
}
