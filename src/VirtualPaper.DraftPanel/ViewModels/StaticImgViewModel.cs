using System;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model.Runtime;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.DraftPanel.ViewModels {
    partial class StaticImgViewModel : ObservableObject {
        internal event EventHandler<double> OnCanvasZoomChanged;

        private double _canvasZoom; // 0.2 -- 7.0
        public double CanvasZoom {
            get { return _canvasZoom; }
            set {
                if (!StaticImgMetadata.IsZoomValid(value) || _canvasZoom == value) return;

                _canvasZoom = value;
                OnPropertyChanged();
                OnCanvasZoomChanged?.Invoke(this, value);
            }
        }

        public LayerManagerData ManagerData { get; } // .vproj (entryFile)

        public StaticImgViewModel(string entryFilePath, FileType rtFileType) {
            _entryFilePath = entryFilePath;
            _rtFileType = rtFileType;
            _basicComponentUtil = new();
            ManagerData = new();
        }

        public async Task SaveAsync() {
            try {
                await JsonSaver.SaveAsync(_entryFilePath, ManagerData, LayerManagerDataContext.Default);
                foreach (var item in ManagerData.LayersData) {
                    await item.SaveAsync(_entryFilePath);
                }
            }
            catch (Exception ex) {
                Draft.Instance.GetNotify().ShowExp(ex);
            }
        }

        public async Task LoadAsync() {
            _basicComponentUtil.Loading(false, false);

            try {
                switch (_rtFileType) {
                    case FileType.FImage:
                        break;
                    case FileType.FProject:
                        await LoadFProjectAsync();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex) {
                Draft.Instance.GetNotify().ShowExp(ex);
            }
            finally {
                _basicComponentUtil.Loaded();
            }
        }

        private async Task LoadFProjectAsync() {
            if (!File.Exists(_entryFilePath)) {
                await ManagerData.SaveAsync(_entryFilePath);
            }
            await ManagerData.LoadAsync(_entryFilePath);
        }

        private readonly FileType _rtFileType;
        private readonly string _entryFilePath = string.Empty;
        internal readonly BasicComponentUtil _basicComponentUtil;
        internal readonly string[] _comboZoomFactors = ["700%", "600%", "500%", "400%", "300%", "200%", "100%", "75%", "50%", "25%"];
    }
}
