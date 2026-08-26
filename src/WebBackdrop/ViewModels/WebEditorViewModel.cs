using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.PlayerWeb.Core.WebView.Windows;
using VirtualPaper.UIComponent.Utils;
using WinUIEx;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;

namespace Workloads.Creation.WebBackdrop.ViewModels {
    public partial class WebEditorViewModel : ObservableObject {
        public event Action? DebugSessionEnded;
        public event Action<string>? FileCacheEvicted;

        public IReadOnlyList<WebEditorFile> CachedFiles => _cahcedFiles;

        public WebEditorFile? ActiveFile {
            get => _activeFile;
            set {
                if (_activeFile == value) return;
                _activeFile = value;
                OnPropertyChanged();
            }
        }

        public WebProjectSession Session { get; }

        public WebEditorViewModel(WebProjectSession session, ArcPageContextKey contextKey) {
            _contextKey = contextKey;
            Session = session;
            _userSettings = AppServiceLocator.Services.GetRequiredService<IUserSettingsClient>();
            _wpControlClient = AppServiceLocator.Services.GetRequiredService<IWallpaperControlClient>();
        }

        public async Task OpenFileAsync(string filePath, CancellationToken cancellationToken = default) {
            if (_cachedFileMap.TryGetValue(filePath, out var existing)) {
                TouchCachedFile(existing);
                ActiveFile = existing;
                if (existing.CanOpenAsText) {
                    Session.FileManager.UpdateSnapshot(filePath);
                }
                return;
            }

            try {
                var file = await WebEditorFile.LoadAsync(filePath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _cahcedFiles.Add(file);
                _cachedFileMap[filePath] = file;
                TouchCachedFile(file);
                ActiveFile = file;
                // 非文本文件（如图片）不建立文档跟踪，避免把二进制读进 Document.Text
                if (file.CanOpenAsText) {
                    Session.FileManager.UpdateSnapshot(filePath);
                }
                TrimFileCache();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error(ex);
                GlobalMessageUtil.ShowError(string.Format(LanguageUtil.GetI18n("WebBackdrop_FailedOpenFile"), filePath, ex.Message));
            }
        }

        private void TouchCachedFile(WebEditorFile file) {
            if (_fileCacheNodes.Remove(file)) { }
            _fileCacheNodes.AddLast(file);
        }

        private void TrimFileCache() {
            long cachedTextBytes = _cahcedFiles.Sum(file => (long)file.Content.Length * sizeof(char));
            while (_cahcedFiles.Count > MaxCachedFileCount || cachedTextBytes > MaxCachedTextBytes) {
                var candidate = _fileCacheNodes.First;
                while (candidate != null
                    && (!candidate.Value.IsSaved || ReferenceEquals(candidate.Value, ActiveFile))) {
                    candidate = candidate.Next;
                }
                if (candidate == null) return;

                var file = candidate.Value;
                _fileCacheNodes.Remove(candidate);
                _cahcedFiles.Remove(file);
                _cachedFileMap.Remove(file.FilePath);
                cachedTextBytes -= (long)file.Content.Length * sizeof(char);
                Session.FileManager.CloseDocument(file.FilePath);
                FileCacheEvicted?.Invoke(file.FilePath);
            }
        }

        public async Task<bool> SaveActiveFileAsync() {
            if (ActiveFile == null) return false;
            return await SaveFileAsync(ActiveFile);
        }

        public bool IsAllSaved => _cahcedFiles.TrueForAll(file => file.IsSaved);

        public WebEditorFile? GetOpenFile(string filePath) {
            return _cachedFileMap.TryGetValue(filePath, out var file) ? file : null;
        }

        /// <summary>文件移动后保留已打开实例、内存内容和活动状态，仅重绑定路径。</summary>
        public WebEditorFile? RebindOpenFilePath(string oldPath, string newPath) {
            if (!_cachedFileMap.Remove(oldPath, out var file)) return null;

            file.RebindPath(newPath);
            _cachedFileMap[newPath] = file;

            Session.FileManager.CloseDocument(oldPath);
            if (file.CanOpenAsText) {
                Session.FileManager.UpdateSnapshot(newPath);
                Session.FileManager.SetDirty(newPath, !file.IsSaved);
            }

            if (ReferenceEquals(ActiveFile, file)) {
                // 实例没有变化，显式通知视图刷新 Monaco 路径和文件树选择。
                OnPropertyChanged(nameof(ActiveFile));
            }
            return file;
        }

        /// <summary>
        /// 关闭已打开的文件：从内存集合移除，并停止 watcher 对该文档的跟踪。
        /// 用于文件被删除等场景，避免一次会话中打开过的文件永久驻留内存。
        /// </summary>
        public void CloseOpenFile(string filePath) {
            if (!_cachedFileMap.TryGetValue(filePath, out var file)) return;

            _cachedFileMap.Remove(filePath);
            _cahcedFiles.Remove(file);
            _fileCacheNodes.Remove(file);
            if (_activeFile == file) {
                ActiveFile = null;
            }
            Session.FileManager.CloseDocument(filePath);
        }

        public async Task<bool> SaveAllAsync() {
            var tasks = _cahcedFiles
                .Where(file => !file.IsSaved)
                .Select(SaveFileAsync);
            var results = await Task.WhenAll(tasks);
            return results.All(result => result);
        }

        public async Task<bool> SaveFileAsync(WebEditorFile file) {
            // 图片等非文本文件不参与编辑器保存，避免把空内容写回文件
            if (!file.CanOpenAsText) return false;

            // 文件加载/重载失败时禁止保存，避免覆盖可能可恢复的原始数据
            if (file.IsLoadFailed) {
                GlobalMessageUtil.ShowError(
                    string.Format(LanguageUtil.GetI18n("WebBackdrop_CannotSaveLoadFailed"), file.FilePath),
                    key: "FileLoadFailed");
                return false;
            }

            try {
                var text = file.Content;
                var enc = file.EncodingText switch {
                    "UTF-8 BOM" => new UTF8Encoding(true),
                    "UTF-16 LE" => Encoding.Unicode,
                    "UTF-16 BE" => Encoding.BigEndianUnicode,
                    _ => new UTF8Encoding(false),
                };

                await FileUtil.WriteAllTextAsync(file.FilePath, text, enc);
                file.MarkAsSaved();

                // Refresh the disk stamp so the FileSystemWatcher won't treat
                // our own save as an external file change.
                Session.FileManager.NotifySaved(file.FilePath);

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error(ex);
                GlobalMessageUtil.ShowError(string.Format(LanguageUtil.GetI18n("WebBackdrop_FailedSaveFile"), file.FilePath, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 另存为（纯拷贝）：把当前内容写入新路径，原文件与编辑器当前文件均保持不变。
        /// 新文件不自动登记进 manifest（不被项目跟踪）。
        /// </summary>
        public async Task<bool> SaveFileAsAsync(WebEditorFile file, string newPath) {
            // 图片等非文本文件不参与编辑器另存为
            if (!file.CanOpenAsText) return false;

            if (file.IsLoadFailed) {
                GlobalMessageUtil.ShowError(
                    string.Format(LanguageUtil.GetI18n("WebBackdrop_CannotSaveLoadFailed"), file.FilePath),
                    key: "FileLoadFailed");
                return false;
            }

            try {
                var enc = file.EncodingText switch {
                    "UTF-8 BOM" => new UTF8Encoding(true),
                    "UTF-16 LE" => Encoding.Unicode,
                    "UTF-16 BE" => Encoding.BigEndianUnicode,
                    _ => new UTF8Encoding(false),
                };

                // 另存为产生的新文件不自动登记进 manifest
                Session.FileManager.IgnoreNextCreated(newPath);
                await FileUtil.WriteAllTextAsync(newPath, file.Content, enc);
                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error(ex);
                GlobalMessageUtil.ShowError(string.Format(LanguageUtil.GetI18n("WebBackdrop_FailedSaveFile"), newPath, ex.Message));
                return false;
            }
        }

        public async Task UpdateRecentUsedAsync(string filePath) {
            if (!string.IsNullOrEmpty(filePath))
                await _userSettings.UpdateRecentUsedAsync(filePath);
        }

        #region Run / Debug

        public async void DebugProject() {
            var context = ArcPageContextManager.GetContext(_contextKey);
            var loadingContext = context?.LoadingContext;
            using var cts = new CancellationTokenSource();

            try {
                if (loadingContext != null) {
                    await loadingContext.RunAsync(DebugProjectAsync, cts);
                }
                else {
                    await DebugProjectAsync(cts.Token);
                }
            }
            catch (OperationCanceledException) {
                CleanupSessions();
                DebugSessionEnded?.Invoke();
                ArcLog.GetLogger<WebEditorViewModel>().Info("Debug cancelled");
            }
            catch (Exception ex) {
                CleanupSessions();
                DebugSessionEnded?.Invoke();
                ArcLog.GetLogger<WebEditorViewModel>().Error($"Debug failed: {ex.Message}");
                GlobalMessageUtil.ShowException(ex);
            }
        }

        private async Task DebugProjectAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await SaveAllAsync()) {
                GlobalMessageUtil.ShowError(LanguageUtil.GetI18n("WebBackdrop_FailedSaveAll"));
                ArcLog.GetLogger<WebEditorViewModel>().Error("Debug: failed to save project files");
                return;
            }

            var projectDir = Session.DesignFileUtil.ProjectFolder;
            if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir)) {
                GlobalMessageUtil.ShowError(LanguageUtil.GetI18n("WebBackdrop_ProjectFolderNotFound"));
                ArcLog.GetLogger<WebEditorViewModel>().Warn("Debug: project folder not found");
                return;
            }

            CleanupDebugSession();

            var srcEntry = Session.DesignFileUtil.EntryFilePath;
            if (File.Exists(srcEntry)) {
                var previewUrl = await Session.PreviewServer.StartAsync(srcEntry, cancellationToken);
                _previewUrl = previewUrl;

                var basicData = new WpBasicData {
                    WallpaperUid = Guid.NewGuid().ToString(),
                    Title = Session.DesignFileUtil.ProjectName,
                    FilePath = srcEntry,
                    FolderPath = projectDir,
                    FolderName = Path.GetFileName(projectDir),
                    FType = FileType.FWebZip,
                };
                // PlayerWeb 会从磁盘读取调试元数据。每个调试会话使用独立的系统临时目录，
                // 避免污染项目文件、触发项目监听或进入 manifest/搜索/导出流程。
                _debugSessionDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "VirtualPaper",
                    "WebBackdrop",
                    "Debug",
                    Guid.NewGuid().ToString("N"));
                var basicDataPath = Path.Combine(
                    _debugSessionDirectory,
                    Constants.Field.WpBasicDataFileName);
                await JsonSaver.SaveAsync(
                    basicDataPath,
                    basicData,
                    WpBasicDataContext.Default);

                cancellationToken.ThrowIfCancellationRequested();
                _debugJsonString = await _wpControlClient.GetPlayerStartArgsAsync(
                    basicData,
                    RuntimeType.RWeb,
                    null,
                    cancellationToken,
                    basicDataPath);

                cancellationToken.ThrowIfCancellationRequested();
                OpenPreviewWindow();
            }

            ArcLog.GetLogger<WebEditorViewModel>().Info($"Debug: preview={_previewUrl}");
        }

        private void OpenPreviewWindow() {
            if (_debugJsonString == null) return;
            var previewWindow = new PreviewWithWeb(_debugJsonString, previewUrl: _previewUrl);
            previewWindow.Closed += (_, _) => {
                _previewWindow = null;
                DebugSessionEnded?.Invoke();
            };
            previewWindow.Show();
            previewWindow.Activate();
            _previewWindow = previewWindow;
        }

        private void CleanupDebugSession() {
            _previewWindow?.Close();
            _previewWindow = null;
            _debugJsonString = null;
            _previewUrl = null;

            var debugSessionDirectory = _debugSessionDirectory;
            _debugSessionDirectory = null;
            try {
                if (!string.IsNullOrWhiteSpace(debugSessionDirectory)
                    && Directory.Exists(debugSessionDirectory)) {
                    Directory.Delete(debugSessionDirectory, recursive: true);
                }
            }
            catch { /* 忽略清理失败 */ }
        }

        public void CleanupSessions() {
            Session.PreviewServer.Stop();
            CleanupDebugSession();
        }

        #endregion

        private WebEditorFile? _activeFile;
        private readonly List<WebEditorFile> _cahcedFiles = [];
        private readonly Dictionary<string, WebEditorFile> _cachedFileMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<WebEditorFile> _fileCacheNodes = [];
        private const int MaxCachedFileCount = 32;
        private const long MaxCachedTextBytes = 64L * 1024 * 1024;
        private readonly IUserSettingsClient _userSettings;
        private readonly IWallpaperControlClient _wpControlClient;
        private string? _debugJsonString;
        private string? _previewUrl;
        private string? _debugSessionDirectory;
        private PreviewWithWeb? _previewWindow;
        private readonly ArcPageContextKey _contextKey;
    }
}
