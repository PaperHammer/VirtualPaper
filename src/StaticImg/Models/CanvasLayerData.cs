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
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.StaticImg.Models.EventArg;
using Workloads.Creation.StaticImg.Views;

namespace Workloads.Creation.StaticImg.Models {
    [JsonSerializable(typeof(CanvasLayerData))]
    internal partial class CanvasLayerDataContext : JsonSerializerContext { }

    internal partial class CanvasLayerData : ObservableObject, IEquatable<CanvasLayerData> {
        public event EventHandler OnDataLoaded;
        public event EventHandler<PathEventArgs> OnDrawsChanging;
        public event EventHandler OnDrawsChanged;

        [JsonIgnore]
        public TaskCompletionSource<bool> RenderCompleted => _renderCompleted;

        private string _name = string.Empty;
        public string Name {
            get => _name;
            set { if (_name == value) return; _name = value; OnPropertyChanged(); }
        }

        public long Tag { get; }
        public bool IsRootBackground { get; }

        uint _background = UintColor.Transparent;
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
                    MainPage.Instance.Bridge.GetNotify().CloseAndRemoveMsg(nameof(Constants.I18n.Draft_SI_LayerLocked));
                OnPropertyChanged();
            }
        }

        private int _zIndex;
        public int ZIndex {
            get { return _zIndex; }
            set { _zIndex = value; OnPropertyChanged(); }
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

        [JsonConstructor]
        [Obsolete("This constructor is intended for JSON deserialization only. Use the another method instead.")]
        public CanvasLayerData(long tag, bool isRootBackground) {
            Tag = tag;
            IsRootBackground = isRootBackground;
        }

        public CanvasLayerData(string filePath, bool isBackground = false) {
            IsRootBackground = isBackground;
            Tag = IdentifyUtil.GenerateIdShort();
            SetFilePath(filePath);
        }

        public CanvasLayerData Copy() {
            CanvasLayerData newLayerData = new(_filePath) {
                Background = this.Background,
                Opacity = this.Opacity,
                IsEnable = this.IsEnable,
                Images = [.. this.Images],
                Draws = [.. this.Draws],
            };

            return newLayerData;
        }

        public async Task SaveAsync() {
            await _saveQueueLock.WaitAsync();
            try {
                //await _drawSaver.SaveToBufferAsync(Draws, filePathForDarws);
                //await _drawSaver.SaveManuallyAsync();
                //await _imageSaver.SaveToBufferAsync(Images, filePathForImages);
                //await _imageSaver.SaveManuallyAsync();

                await MessagePackSaver.SaveAsync(_filePathForDarws, Draws);
                await MessagePackSaver.SaveAsync(_filePathForImages, Images);
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Project_STI_LayerSaveFailed), InfoBarType.Error, Name, Tag.ToString(), false);
            }
            finally {
                _saveQueueLock.Release();
            }
        }

        public async Task LoadAsync() {
            try {
                this.Draws = await MessagePackSaver.LoadAsync<ObservableCollection<STADraw>>(_filePathForDarws) ?? [];
                this.Images = await MessagePackSaver.LoadAsync<ObservableCollection<STAImage>>(_filePathForImages) ?? [];
                OnDataLoaded?.Invoke(this, EventArgs.Empty);

                await RenderCompleted.Task;
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Project_STI_LayerLoadFailed), InfoBarType.Error, Name, Tag.ToString(), false);
            }
        }

        internal void AddDraw(Path currentPath, STADraw currentDraw) {
            Draws.Add(currentDraw);
            OnDrawsChanging?.Invoke(this, new(currentPath, OperationType.Add));
        }

        internal void RemoveDraw(Path currentPath, STADraw currentDraw) {
            Draws.Remove(currentDraw);
            OnDrawsChanging?.Invoke(this, new(currentPath, OperationType.Remove));
        }

        internal void DrawsChanged() {
            OnDrawsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetFilePath(string filePath) {
            _filePath = filePath;
            string newFolderPath = System.IO.Path.GetDirectoryName(filePath);
            _filePathForDarws = System.IO.Path.Combine(newFolderPath, Tag + ".draws");
            _filePathForImages = System.IO.Path.Combine(newFolderPath, Tag + ".images");
        }

        internal async Task DeletAsync() {
            await FileUtil.TryDeleteFileAsync(_filePathForDarws, 0, 0);
            await FileUtil.TryDeleteFileAsync(_filePathForImages, 0, 0);
        }

        public bool Equals(CanvasLayerData other) {
            return this.Tag == other.Tag;
        }

        private string _filePath, _filePathForDarws, _filePathForImages;
        private readonly SemaphoreSlim _saveQueueLock = new(1, 1);
        private readonly TaskCompletionSource<bool> _renderCompleted = new();
        //private readonly BufferSaver<ObservableCollection<STAImage>> _imageSaver = new();
        //private readonly BufferSaver<ObservableCollection<STADraw>> _drawSaver = new();
    }
}
