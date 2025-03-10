using System;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Panels;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.ViewModels;

namespace VirtualPaper.DraftPanel.ViewModels {
    internal partial class ProjectRunViewModel : ObservableObject {
        private object _frameContent;
        public object FrameContent {
            get { return _frameContent; }
            set { _frameContent = value; OnPropertyChanged(); }
        }

        public ProjectRunViewModel(string filePath) {
            this._filePath = filePath;
            _loadingViewModel = new();
        }

        internal async Task InitProjectAsync() {
            _loadingViewModel.Loading(false, false);
            await LoadRuntimePanelAsync();
            _loadingViewModel.Loaded();
        }

        private async Task LoadRuntimePanelAsync() {
            string folderPath = Path.GetDirectoryName(_filePath);
            string extension = Path.GetExtension(_filePath);
            FileType determinedFileType = FileFilter.DetermineFileType(extension);

            IRuntime data;
            switch (determinedFileType) {
                case FileType.FImage:
                    data = new StaticImg(folderPath);
                    await data.LoadAsync();
                    FrameContent = data;
                    break;
                case FileType.FGif:
                    break;
                case FileType.FVideo:
                    break;
                case FileType.FDesign:
                    data = new StaticImg(folderPath);
                    await data.LoadAsync();
                    FrameContent = data;
                    break;
                case FileType.FProject:
                    data = new StaticImg(folderPath);
                    await data.LoadAsync();
                    FrameContent = data;
                    break;
                default:
                    break;
            }
        }

        internal async void Save() {
            if (FrameContent is not IRuntime irt) return;
            await irt.SaveAsync();
        }

        internal void Exit() {
            throw new NotImplementedException();
        }

        internal readonly LoadingViewModel _loadingViewModel;
        private readonly string _filePath;
    }
}
