using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.Mvvm;

namespace Workloads.Creation.StaticImg.Models {
    [JsonSerializable(typeof(InkCanvasData))]
    public partial class InkCanvasDataContext : JsonSerializerContext { }

    public partial class InkCanvasData : ObservableObject {
        [JsonIgnore]
        public InkRenderData Render { get; set; }
        public long Tag { get; }
        public bool IsRootBackground { get; }

        private string _name = string.Empty;
        public string Name {
            get => _name;
            set { if (_name == value) return; _name = value; OnPropertyChanged(); }
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
            set { _zIndex = value; }
        }

        ImageSource _layerThum;
        [JsonIgnore]
        public ImageSource LayerThum {
            get { return _layerThum; }
            set { _layerThum = value; OnPropertyChanged(); }
        }

        [JsonConstructor]
        [Obsolete("This constructor is intended for JSON deserialization only. Use the another method instead.")]
        internal InkCanvasData(long tag, bool isRootBackground) {
            Tag = tag;
            IsRootBackground = isRootBackground;
        }

        public InkCanvasData(string entryFilePath, bool isRootBackground = false) {
            Tag = IdentifyUtil.GenerateIdShort();
            IsRootBackground = isRootBackground;
            SetDataFilePath(entryFilePath);
        }

        internal async Task SaveAsync() {
            try {
                await Render.SaveWithProgressAsync(_dataFilePath, null, default);
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Project_STI_LayerSaveFailed), InfoBarType.Error, Name, Tag.ToString(), false);
            }
        }

        internal async Task LoadAsync() {
            try {
                await Render.LoadWithProgressAsync(_dataFilePath, null, default);
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Project_STI_LayerLoadFailed), InfoBarType.Error, Name, Tag.ToString(), false);
            }
        }

        internal void SetDataFilePath(object filePath) {
            string folder = System.IO.Path.GetDirectoryName(filePath.ToString()) ?? string.Empty;
            _dataFilePath = System.IO.Path.Combine(folder, Tag + "._data");
        }

        internal async Task DeletAsync() {
            await FileUtil.TryDeleteFileAsync(_dataFilePath, 0, 0);
        }

        internal InkCanvasData Clone() {
            var newInk = new InkCanvasData(_dataFilePath, IsRootBackground) {
                Name = Name,
                Opacity = Opacity,
                IsEnable = IsEnable,
                Render = Render.Clone()
            };
            return newInk;
        }

        private string _dataFilePath;
    }
}
