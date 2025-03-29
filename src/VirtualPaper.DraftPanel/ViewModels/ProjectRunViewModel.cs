//using System;
//using System.IO;
//using System.Threading.Tasks;
//using VirtualPaper.Common;
//using VirtualPaper.Common.Utils.Files;
//using VirtualPaper.DraftPanel.Model.Interfaces;
//using VirtualPaper.DraftPanel.Panels;
//using VirtualPaper.DraftPanel.Views.WorkSpaceComponents;
//using VirtualPaper.Models.Mvvm;

//namespace VirtualPaper.DraftPanel.ViewModels {
//    internal partial class ProjectRunViewModel : ObservableObject {
//        private object _frameContent;
//        public object FrameContent {
//            get { return _frameContent; }
//            set { _frameContent = value; OnPropertyChanged(); }
//        }

//        internal async void InitProjectAsync() {
//            ProjectRun.BasicComp.Loading(false, false);
//            LoadRuntimePanelAsync();
//            ProjectRun.BasicComp.Loaded();
//        }

//        private async void LoadRuntimePanelAsync() {
//            string extension = Points.GetExtension(ProjectRun.ProjectFilePath);
//            FileType determinedFileType = FileFilter.GetRuntimeFileType(extension);

//            IRuntime data;
//            switch (determinedFileType) {
//                case FileType.FImage:
//                    data = new StaticImg();
//                    FrameContent = data;
//                    //await data.LoadAsync();                    
//                    break;
//                case FileType.FGif:
//                    break;
//                case FileType.FVideo:
//                    break;
//                case FileType.FDesign:
//                    data = new StaticImg();
//                    FrameContent = data;
//                    //await data.LoadAsync();                    
//                    break;
//                case FileType.FProject:
//                    data = new StaticImg();
//                    FrameContent = data;
//                    //await data.LoadAsync();                    
//                    break;
//                default:
//                    break;
//            }
//        }

//        internal async void Save() {
//            if (FrameContent is not IRuntime irt) return;
//            await irt.SaveAsync();
//        }

//        internal void ExitAsync() {
//            throw new NotImplementedException();
//        }
//    }
//}
