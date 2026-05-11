using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using VirtualPaper.Common;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.ML.StyleTransfer;
using VirtualPaper.ML.SuperResolution;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.IntelligentPanel.ViewModels {
    public partial class StyleTranferViewModel : ObservableObject {
        public ObservableCollection<StyleTransferOutput> Tasks { get; } = [];

        private bool _hasTasks;
        public bool HasTasks {
            get => _hasTasks;
            set {
                if (_hasTasks == value) return;

                _hasTasks = value;
                OnPropertyChanged();
            }
        }

        public StyleTranferViewModel() {
            InitEvent();

            Tasks.Add(new StyleTransferOutput(@"C:\Windows\Web\Wallpaper\Windows\img0.jpg", @"C:\Windows\Web\Wallpaper\Windows\img19.jpg"));
            Tasks.Add(new StyleTransferOutput(@"C:\Windows\Web\Wallpaper\Windows\img0.jpg", @"C:\Windows\Web\Wallpaper\Windows\img19.jpg"));
            Tasks.Add(new StyleTransferOutput(@"C:\Windows\Web\Wallpaper\Windows\img0.jpg", @"C:\Windows\Web\Wallpaper\Windows\img19.jpg"));
        }

        private void InitEvent() {
            Tasks.CollectionChanged += OnTasksCollectionChanged;
        }

        internal bool AddTask(StyleTransferInput data) {
            if (data == null || string.IsNullOrEmpty(data.SourceFilePath) || string.IsNullOrEmpty(data.StyleFilePath)) return false;

            string tmpOutPath_style = Path.Combine(Constants.CommonPaths.TempDir, Path.GetRandomFileName(), Path.GetExtension(data.SourceFilePath));
            string tmpOutPath_realeargan = Path.Combine(Constants.CommonPaths.TempDir, Path.GetRandomFileName(), Path.GetExtension(data.SourceFilePath));
            Tasks.Add(new StyleTransferOutput(data.SourceFilePath, data.StyleFilePath));

            //AdaIn.TransferStyle(data.SourceFilePath, data.StyleFilePath, tmpOutPath_style);
            //Realesrgan.Upscale(tmpOutPath_style, tmpOutPath_realeargan, data.Width, data.Height);

            return true;
        }

        private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
            HasTasks = Tasks.Count > 0;
        }

        private bool _disposed;
        public void Dispose() {
            if (_disposed) return;

            Tasks.CollectionChanged -= OnTasksCollectionChanged;
            Tasks.Clear();
            _disposed = true;
        }
    }
}
