using System;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.StaticImg.Models;

namespace Workloads.Creation.StaticImg.ViewModels {
    internal partial class InkCanvasViewModel : ObservableObject {
        internal event EventHandler RequestFullRender;
        internal event EventHandler SeletcedToolChanging;
        internal event EventHandler SeletcedLayerChanged;
        internal event EventHandler<double> SeletcedCropAspectCliked;
        internal event EventHandler Rebuild;
        internal TaskCompletionSource<bool> BasicDataLoaded => _basicDataLoaded;

        private LayerBasicData _basicData;
        public LayerBasicData BasicData {
            get { return _basicData; }
            set { _basicData = value; }
        }

        public InkCanvasViewModel(string entryFilePath, FileType fileType) {
            _fileType = fileType;
            _entryFilePath = entryFilePath;
            BasicData = new(entryFilePath);
            BasicData.InkDataEnabledChanged += OnInkDataEnabledChanged;
            BasicData.SeletcedToolChanged += OnSeletcedToolChanged;
            BasicData.SeletcedLayerChanged += OnSeletcedLayerChanged;
            BasicData.SeletcedCropAspectClicked += OnSeletcedCropAspectClicked;
            BasicData.Rebuild += OnRebuild;
        }

        private void OnRebuild(object sender, EventArgs e) {
            Rebuild?.Invoke(this, EventArgs.Empty);
        }

        private void OnSeletcedCropAspectClicked(object sender, double e) {
            SeletcedCropAspectCliked?.Invoke(this, e);
        }

        private void OnSeletcedLayerChanged(object sender, EventArgs e) {
            SeletcedLayerChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnSeletcedToolChanged(object sender, EventArgs e) {
            SeletcedToolChanging?.Invoke(this, EventArgs.Empty);
        }

        private void OnInkDataEnabledChanged(object sender, EventArgs e) {
            Rebuild?.Invoke(this, EventArgs.Empty);
        }

        internal async Task SaveAsync() {
            await BasicData.SaveBasicAsync();
            await BasicData.SaveRenderDataAsync();
        }

        internal async Task LoadBasicOrInit() {
            try {
                switch (_fileType) {
                    case FileType.FImage:
                        break;
                    case FileType.FProject:
                        await LoadProjectAsync();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowExp(ex);
            }
        }

        private async Task LoadProjectAsync() {
            if (!File.Exists(_entryFilePath)) {
                await BasicData.InitDataAsync();
                await BasicData.SaveRenderDataAsync();
            }
            await BasicData.LoadBasicDataAsync();
            BasicDataLoaded.TrySetResult(true);
        }

        internal async Task LoadRenderDataAsync() {
            await BasicData.LoadRenderDataAsync();
            RequestFullRender?.Invoke(this, EventArgs.Empty);
        }

        private readonly string _entryFilePath;
        private readonly FileType _fileType;
        private readonly TaskCompletionSource<bool> _basicDataLoaded = new();
    }
}
