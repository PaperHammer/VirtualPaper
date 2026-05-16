using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.ML.StyleTransfer.Interfaces;
using VirtualPaper.ML.SuperResolution.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.PanelBus.WpSettingsArgs;

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
            taskItem.SaveCommand = new RelayCommand(async () => await SaveResultAsync(taskItem), () => taskItem.IsCompleted);
            taskItem.ImportCommand = new RelayCommand(() => ImportResult(taskItem), () => taskItem.IsCompleted);

            Tasks.Add(taskItem);
            _ = ProcessTaskAsync(taskItem);

            return true;
        }

        private async Task ProcessTaskAsync(StyleTransferTaskItem taskItem) {
            var ct = taskItem.Cts.Token;
            string? tempDir = null;

            taskItem.Status = TaskStatus.WaitingToRun;

            try {
                // ── 等待信号量（排队阶段，可取消）──
                await _concurrencyGate.WaitAsync(ct);
            }
            catch (OperationCanceledException) {
                // 还没拿到信号量就被取消了（排队中删除），无需 Release
                CleanupAfterCancel(taskItem, tempDir);
                return;
            }

            try {
                ct.ThrowIfCancellationRequested();

                taskItem.Status = TaskStatus.Running;

                var data = taskItem.Data;
                tempDir = Path.Combine(Constants.CommonPaths.TempDir, Path.GetRandomFileName());
                Directory.CreateDirectory(tempDir);

                string ext = Path.GetExtension(data.SourceFilePath);
                string tmpOutPath_style = Path.Combine(tempDir, $"styled{ext}");
                string tmpOutPath_realesrgan = Path.Combine(tempDir, $"upscaled{ext}");

                await Task.Run(() => {
                    ct.ThrowIfCancellationRequested();

                    var adain = AppServiceLocator.Services.GetRequiredService<IStyleTransfer>();
                    adain.LoadModel();

                    ct.ThrowIfCancellationRequested();

                    adain.RunAndSave(
                        data.SourceFilePath,
                        data.StyleFilePath,
                        tmpOutPath_style,
                        ct: ct);

                    ct.ThrowIfCancellationRequested();

                    var superResolution = AppServiceLocator.Services.GetRequiredService<ISuperResolution>();
                    superResolution.LoadModel();

                    ct.ThrowIfCancellationRequested();

                    superResolution.RunAndSave(
                        tmpOutPath_style,
                        tmpOutPath_realesrgan,
                        (uint)data.Width,
                        (uint)data.Height,
                        ct);

                    ct.ThrowIfCancellationRequested();

                    data.SetResultAsync(tmpOutPath_realesrgan).Wait();
                }, ct);

                ct.ThrowIfCancellationRequested();

                taskItem.NotifyResultChanged();
                taskItem.Status = TaskStatus.RanToCompletion;

                _ = FileUtil.TryDeleteFileAsync(tmpOutPath_style);
            }
            catch (OperationCanceledException) {
                CleanupAfterCancel(taskItem, tempDir);
                GlobalMessageUtil.ShowCanceled();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<StyleTranferViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
                taskItem.Status = TaskStatus.Faulted;
            }
            finally {
                _concurrencyGate.Release();
            }
        }

        /// <summary>
        /// 任务被取消后的清理工作
        /// </summary>
        private static void CleanupAfterCancel(StyleTransferTaskItem taskItem, string? tempDir) {
            if (tempDir != null) {
                try {
                    if (Directory.Exists(tempDir)) {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch {
                    // 清理失败不影响主流程
                }
            }

            // 清理结果文件
            if (taskItem.Data.ResultFilePath != null) {
                _ = FileUtil.TryDeleteFileAsync(taskItem.Data.ResultFilePath);
            }
        }

        private void RemoveTask(StyleTransferTaskItem taskItem) {
            // 发出取消信号（如果任务还在运行或排队）
            if (!taskItem.Cts.IsCancellationRequested) {
                taskItem.Cts.Cancel();
            }
            taskItem.Cts.Dispose();

            Tasks.Remove(taskItem);

            // 对于已完成的任务，结果文件也需要清理
            if (taskItem.IsCompleted && taskItem.Data.ResultFilePath != null) {
                _ = FileUtil.TryDeleteFileAsync(taskItem.Data.ResultFilePath);
            }
        }

        private async void PreviewResult(StyleTransferTaskItem taskItem) {
            if (string.IsNullOrEmpty(taskItem.Data.ResultFilePath)) {
                GlobalMessageUtil.ShowError(nameof(Constants.I18n.Text_File_Not_Available), isNeedLocalizer: true);
                return;
            }

            var (found, _) = await PanelMessageCenter.TryInvokeAsync<PreviewFileArgs, bool>(
                PanelContracts.WpSettings.Id,
                PanelContracts.WpSettings.Action_PreviewFile,
                new PreviewFileArgs(taskItem.Data.ResultFilePath, ArcPageContextManager.GetContext<Intelligent>()));

            if (!found) {
                GlobalMessageUtil.ShowError("Panel is not available.", isNeedLocalizer: false);
                ArcLog.GetLogger<StyleTranferViewModel>().Error("WpSettings panel is not available for previewing file.");
            }
        }

        private async Task SaveResultAsync(StyleTransferTaskItem taskItem) {
            if (string.IsNullOrEmpty(taskItem.Data.ResultFilePath)) {
                GlobalMessageUtil.ShowError(nameof(Constants.I18n.Text_File_Not_Available), isNeedLocalizer: true);
                return;
            }

            var suggestFilename = Path.GetFileName(taskItem.SourceFilePath);
            var saveFile = await WindowsStoragePickers.PickSaveFileAsync(
                WindowConsts.WindowHandle,
                suggestFilename,
                new Dictionary<string, string[]>() {
                    [taskItem.SourceFileExt[..1].ToUpper()] = [taskItem.SourceFileExt]
                }
            );

            if (saveFile == null || string.IsNullOrEmpty(saveFile.Path))
                return;

            try {
                await Task.Run(() => File.Copy(taskItem.Data.ResultFilePath, saveFile.Path, overwrite: true));
                GlobalMessageUtil.ShowSuccess($"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Export_Success))} {saveFile.Path}");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<StyleTranferViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        private async void ImportResult(StyleTransferTaskItem taskItem) {
            if (string.IsNullOrEmpty(taskItem.Data.ResultFilePath)) {
                GlobalMessageUtil.ShowError(nameof(Constants.I18n.Text_File_Not_Available), isNeedLocalizer: true);
                return;
            }

            var (found, success) = await PanelMessageCenter.TryInvokeAsync<string, bool>(
                PanelContracts.WpSettings.Id,
                PanelContracts.WpSettings.Action_ImportWallpaper,
                taskItem.Data.ResultFilePath);

            if (!found) {
                GlobalMessageUtil.ShowError("Panel is not available.", isNeedLocalizer: false);
                ArcLog.GetLogger<StyleTranferViewModel>().Error("WpSettings panel is not available for previewing file.");
                return;
            }

            if (success)
                GlobalMessageUtil.ShowSuccess(LanguageUtil.GetI18n(nameof(Constants.I18n.Add_To_Lib_Success)));
            else
                GlobalMessageUtil.ShowError(Constants.I18n.InfobarMsg_ImportErr, isNeedLocalizer: true);
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
        /// 最多同时执行 3 个任务，避免占用过多的 CPU 与 内存
        /// </summary>
        private readonly SemaphoreSlim _concurrencyGate = new(MaxConcurrency, MaxConcurrency);
        private const int MaxConcurrency = 3;
    }
}
