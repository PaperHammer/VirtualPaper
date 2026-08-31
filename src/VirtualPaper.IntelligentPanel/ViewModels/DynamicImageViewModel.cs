using System;
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
using VirtualPaper.ML.DepthEstimate.Models;
using VirtualPaper.ML.DynamicImage;
using VirtualPaper.ML.DynamicImage.Models;
using VirtualPaper.ML.ObjectDetection.Models;
using VirtualPaper.ML.Segmentation.Models;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.PanelBus.WpSettingsArgs;

namespace VirtualPaper.IntelligentPanel.ViewModels {
    public sealed class DynamicImageViewModel : ObservableObject, IDisposable {
        public ObservableCollection<DynamicImageTaskItem> Tasks { get; } = [];

        private bool _hasTasks;
        public bool HasTasks {
            get => _hasTasks;
            private set {
                if (_hasTasks == value)
                    return;
                _hasTasks = value;
                OnPropertyChanged();
            }
        }

        public DynamicImageViewModel() => Tasks.CollectionChanged += OnTasksChanged;

        internal bool AddTask(DynamicImageData data) {
            if (data is null || string.IsNullOrWhiteSpace(data.SourceFilePath))
                return false;

            var item = new DynamicImageTaskItem(data);
            item.RemoveCommand = new RelayCommand(() => RemoveTask(item));
            item.PreviewCommand = new RelayCommand(
                () => PreviewResult(item),
                () => item.IsCompleted);
            item.ExportCommand = new RelayCommand(
                async () => await ExportResultAsync(item),
                () => item.IsCompleted);
            item.OpenFolderCommand = new RelayCommand(
                () => OpenResultFolder(item),
                () => item.IsCompleted);
            Tasks.Add(item);
            _ = ProcessTaskAsync(item);
            return true;
        }

        private async Task ProcessTaskAsync(DynamicImageTaskItem item) {
            CancellationToken ct = item.Cts.Token;
            string? tempDirectory = null;
            item.Status = TaskStatus.WaitingToRun;

            try {
                await _concurrencyGate.WaitAsync(ct);
            }
            catch (OperationCanceledException) {
                return;
            }

            try {
                ct.ThrowIfCancellationRequested();
                item.Status = TaskStatus.Running;
                tempDirectory = Path.Combine(
                    Constants.CommonPaths.TempDir,
                    "dynamic-image",
                    item.Id.ToString("N"));
                string outputDirectory = Path.Combine(tempDirectory, "output");
                Directory.CreateDirectory(outputDirectory);

                await Task.Run(() => {
                    ct.ThrowIfCancellationRequested();
                    using DynamicImageAnalyzer analyzer =
                        AppServiceLocator.Services.GetRequiredService<DynamicImageAnalyzer>();
                    analyzer.LoadModels();
                    DynamicImageAnalysisResult analysis = analyzer.Analyze(
                        item.Data.SourceFilePath,
                        CreateOptions(item.Data.Quality),
                        ct);
                    DynamicImageExportResult export = DynamicImageExporter.Export(
                        item.Data.SourceFilePath,
                        analysis,
                        outputDirectory,
                        ct);
                    item.Data.SetResult(export, analysis);
                }, ct);

                ct.ThrowIfCancellationRequested();
                item.NotifyResultChanged();
                item.Status = TaskStatus.RanToCompletion;
            }
            catch (OperationCanceledException) {
                CleanupDirectory(tempDirectory);
                GlobalMessageUtil.ShowCanceled();
            }
            catch (Exception ex) {
                CleanupDirectory(tempDirectory);
                ArcLog.GetLogger<DynamicImageViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
                item.Status = TaskStatus.Faulted;
            }
            finally {
                _concurrencyGate.Release();
            }
        }

        internal static DynamicImageAnalysisOptions CreateOptions(DynamicImageQuality quality) =>
            quality == DynamicImageQuality.High
                ? new DynamicImageAnalysisOptions {
                    Detection = new ObjectDetectionOptions {
                        InputWidth = 960,
                        InputHeight = 960,
                        ScoreThreshold = 0.25f
                    },
                    Segmentation = new MobileSamOptions { MaxBoxes = 12 },
                    Depth = new DepthAnythingOptions {
                        InputSize = 686,
                        ResizeMode = DepthAnythingResizeMode.FitLongestSide
                    },
                    Fusion = new LayerFusionOptions { MaxObjects = 12 }
                }
                : new DynamicImageAnalysisOptions {
                    Detection = new ObjectDetectionOptions {
                        InputWidth = 640,
                        InputHeight = 640,
                        ScoreThreshold = 0.3f
                    },
                    Segmentation = new MobileSamOptions { MaxBoxes = 8 },
                    Depth = new DepthAnythingOptions {
                        InputSize = 518,
                        ResizeMode = DepthAnythingResizeMode.FitLongestSide
                    },
                    Fusion = new LayerFusionOptions { MaxObjects = 8 }
                };

        private void RemoveTask(DynamicImageTaskItem item) {
            if (!item.Cts.IsCancellationRequested)
                item.Cts.Cancel();
            Tasks.Remove(item);
            CleanupDirectory(GetTaskRoot(item));
        }

        private async void PreviewResult(DynamicImageTaskItem item) {
            if (string.IsNullOrWhiteSpace(item.PreviewFilePath) || !File.Exists(item.PreviewFilePath)) {
                GlobalMessageUtil.ShowError(
                    nameof(Constants.I18n.Text_File_Not_Available),
                    isNeedLocalizer: true);
                return;
            }

            var (found, _) = await PanelMessageCenter.TryInvokeAsync<PreviewFileArgs, bool>(
                PanelContracts.WpSettings.Id,
                PanelContracts.WpSettings.Action_PreviewFile,
                new PreviewFileArgs(
                    item.PreviewFilePath,
                    ArcPageContextManager.GetContext<Intelligent>()));
            if (!found)
                GlobalMessageUtil.ShowError("Text_PanelUnavailable", isNeedLocalizer: true);
        }

        private static void OpenResultFolder(DynamicImageTaskItem item) {
            if (!string.IsNullOrWhiteSpace(item.ResultDirectory))
                FileUtil.OpenFolderByExplorer(item.ResultDirectory);
        }

        private static async Task ExportResultAsync(DynamicImageTaskItem item) {
            if (string.IsNullOrWhiteSpace(item.ResultDirectory) ||
                !Directory.Exists(item.ResultDirectory)) {
                GlobalMessageUtil.ShowError(
                    nameof(Constants.I18n.Text_File_Not_Available),
                    isNeedLocalizer: true);
                return;
            }

            var folder = await WindowsStoragePickers.PickFolderAsync(WindowConsts.WindowHandle);
            if (folder is null || string.IsNullOrWhiteSpace(folder.Path))
                return;

            try {
                string baseName = $"{Path.GetFileNameWithoutExtension(item.SourceFilePath)}_dynamic";
                string destination = FileUtil.NextAvailablePath(Path.Combine(folder.Path, baseName));
                await Task.Run(() => FileUtil.CopyDirectory(
                    item.ResultDirectory,
                    destination,
                    copySubDirs: true));
                GlobalMessageUtil.ShowSuccess(
                    $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Export_Success))} {destination}");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<DynamicImageViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        private static string GetTaskRoot(DynamicImageTaskItem item) =>
            Path.Combine(Constants.CommonPaths.TempDir, "dynamic-image", item.Id.ToString("N"));

        private static void CleanupDirectory(string? directory) {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;
            _ = FileUtil.TryDeleteDirectoryAsync(directory);
        }

        private void OnTasksChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
            HasTasks = Tasks.Count != 0;

        public void Dispose() {
            Tasks.CollectionChanged -= OnTasksChanged;
            foreach (DynamicImageTaskItem item in Tasks) {
                if (!item.Cts.IsCancellationRequested)
                    item.Cts.Cancel();
                CleanupDirectory(GetTaskRoot(item));
            }
            Tasks.Clear();
        }

        private readonly SemaphoreSlim _concurrencyGate = new(1, 1);
    }
}
