using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.ML.StyleTransfer;
using VirtualPaper.ML.SuperResolution;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.IntelligentPanel.ViewModels {
    public partial class StyleTranferViewModel : ObservableObject {
        public ObservableCollection<StyleTransferTaskItem> Tasks { get; } = [];

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

        internal bool AddTask(StyleTransferData data) {
            if (data == null || string.IsNullOrEmpty(data.SourceFilePath) || string.IsNullOrEmpty(data.StyleFilePath)) return false;

            var taskItem = new StyleTransferTaskItem(data);
            taskItem.RemoveCommand = new RelayCommand(() => RemoveTask(taskItem));
            taskItem.PreviewCommand = new RelayCommand(() => PreviewResult(taskItem), () => taskItem.IsCompleted);
            taskItem.SaveCommand = new RelayCommand(() => SaveResult(taskItem), () => taskItem.IsCompleted);
            taskItem.ImportCommand = new RelayCommand(() => ImportResult(taskItem), () => taskItem.IsCompleted);

            Tasks.Add(taskItem);
            _ = ProcessTaskAsync(taskItem);

            return true;
        }

        private async Task ProcessTaskAsync(StyleTransferTaskItem taskItem) {
            taskItem.Status = TaskStatus.WaitingToRun;
            taskItem.IsIndeterminate = false;
            taskItem.IsShowError = false;

            await _concurrencyGate.WaitAsync();

            var stopwatch = Stopwatch.StartNew();
            try {
                taskItem.Status = TaskStatus.Running;
                taskItem.IsIndeterminate = true;

                var data = taskItem.Data;

                string tempDir = Path.Combine(Constants.CommonPaths.TempDir, Path.GetRandomFileName());
                Directory.CreateDirectory(tempDir);

                string ext = Path.GetExtension(data.SourceFilePath);
                string tmpOutPath_style = Path.Combine(tempDir, $"styled{ext}");
                string tmpOutPath_realesrgan = Path.Combine(tempDir, $"upscaled{ext}");

                await Task.Run(() => {
                    AdaIn.TransferStyle(data.SourceFilePath, data.StyleFilePath, tmpOutPath_style);
                    Realesrgan.Upscale(tmpOutPath_style, tmpOutPath_realesrgan, data.Width, data.Height);
                });

                stopwatch.Stop();
                data.SetResult(tmpOutPath_realesrgan);
                taskItem.NotifyResultChanged();
                taskItem.IsIndeterminate = false;
                taskItem.Status = TaskStatus.RanToCompletion;

                _ = FileUtil.TryDeleteFileAsync(tmpOutPath_style);
            }
            catch (Exception ex) {
                stopwatch.Stop();
                ArcLog.GetLogger<StyleTranferViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
                taskItem.IsIndeterminate = false;
                taskItem.IsShowError = true;
                taskItem.Status = TaskStatus.Faulted;
            }
            finally {
                _concurrencyGate.Release();
            }
        }

        private void RemoveTask(StyleTransferTaskItem taskItem) {
            Tasks.Remove(taskItem);
             if (taskItem.Data.ResultFilePath != null) _ = FileUtil.TryDeleteFileAsync(taskItem.Data.ResultFilePath);
        }

        private void PreviewResult(StyleTransferTaskItem taskItem) {
            // TODO: 打开预览窗口
        }

        private void SaveResult(StyleTransferTaskItem taskItem) {
            // TODO: 保存到用户选择的路径
        }

        private void ImportResult(StyleTransferTaskItem taskItem) {
            // TODO: 入库为壁纸
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

        /// <summary>
        /// 最多同时执行 3 个任务
        /// </summary>
        private readonly SemaphoreSlim _concurrencyGate = new(MaxConcurrency, MaxConcurrency);
        private const int MaxConcurrency = 3;
    }
}
