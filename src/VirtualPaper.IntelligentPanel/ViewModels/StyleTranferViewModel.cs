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
        private readonly ObservableCollection<StyleTransferOutput> Tasks = [];

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
        }

        private void InitEvent() {
            Tasks.CollectionChanged += OnTasksCollectionChanged;
        }

        internal bool AddTask(StyleTransferInput input) {
            if (input == null || string.IsNullOrEmpty(input.SourceFilePath) || string.IsNullOrEmpty(input.StyleFilePath)) return false;

            string tmpOutPath_style = Path.Combine(Constants.CommonPaths.TempDir, Path.GetRandomFileName(), Path.GetExtension(input.SourceFilePath));
            string tmpOutPath_realeargan = Path.Combine(Constants.CommonPaths.TempDir, Path.GetRandomFileName(), Path.GetExtension(input.SourceFilePath));
            Tasks.Add(new StyleTransferOutput(input.SourceFilePath, input.StyleFilePath));

            AdaIn.TransferStyle(input.SourceFilePath, input.StyleFilePath, tmpOutPath_style);
            Realesrgan.Upscale(tmpOutPath_style, tmpOutPath_realeargan, input.Width, input.Height);

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
