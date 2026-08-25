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

        public IReadOnlyList<WebEditorFile> OpenFiles => _openFiles;

        public WebEditorFile? ActiveFile {
            get => _activeFile;
            set {
                if (_activeFile == value) return;
                _activeFile = value;
                OnPropertyChanged();
            }
        }

        // [已废弃，暂注释] 只写不读（绑定回写后无消费方）
        /*
        private WebFileItem? _selectedFileItem;
        public WebFileItem? SelectedFileItem {
            get { return _selectedFileItem; }
            set { if (_selectedFileItem == value) return; _selectedFileItem = value; OnPropertyChanged(); }
        }
        */

        public WebProjectSession Session { get; }

        // [已废弃，暂注释] WebToolListControl 未被任何视图使用
        /*
        public readonly List<WebToolItem> ToolItems = [
            new() { Type = WebToolType.FileTree,    ToolName = "Project_WebBackdrop_ToolName_FileTree",    Glyph = "\uE8B7" },
            new() { Type = WebToolType.ProjectInfo, ToolName = "Project_WebBackdrop_ToolName_ProjectInfo", Glyph = "\uE946" },
        ];
        */

        public WebEditorViewModel(WebProjectSession session, ArcPageContextKey contextKey) {
            _contextKey = contextKey;
            Session = session;
            _userSettings = AppServiceLocator.Services.GetRequiredService<IUserSettingsClient>();
            _wpControlClient = AppServiceLocator.Services.GetRequiredService<IWallpaperControlClient>();
        }

        public async Task OpenFileAsync(string filePath) {
            if (_openFileMap.TryGetValue(filePath, out var existing)) {
                ActiveFile = existing;
                if (existing.CanOpenAsText) {
                    Session.FileManager.UpdateSnapshot(filePath);
                }
                return;
            }

            try {
                var file = await WebEditorFile.LoadAsync(filePath);
                _openFiles.Add(file);
                _openFileMap[filePath] = file;
                ActiveFile = file;
                // 非文本文件（如图片）不建立文档跟踪，避免把二进制读进 Document.Text
                if (file.CanOpenAsText) {
                    Session.FileManager.UpdateSnapshot(filePath);
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error(ex);
                GlobalMessageUtil.ShowError($"Failed to open file: {filePath}\nThe file may be corrupted or unreadable.\n{ex.Message}");
            }
        }

        public async Task<bool> SaveActiveFileAsync() {
            if (ActiveFile == null) return false;
            return await SaveFileAsync(ActiveFile);
        }

        public bool IsAllSaved => _openFiles.TrueForAll(file => file.IsSaved);

        public WebEditorFile? GetOpenFile(string filePath) {
            return _openFileMap.TryGetValue(filePath, out var file) ? file : null;
        }

        /// <summary>文件移动后保留已打开实例、内存内容和活动状态，仅重绑定路径。</summary>
        public WebEditorFile? RebindOpenFilePath(string oldPath, string newPath) {
            if (!_openFileMap.Remove(oldPath, out var file)) return null;

            file.RebindPath(newPath);
            _openFileMap[newPath] = file;

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
            if (!_openFileMap.TryGetValue(filePath, out var file)) return;

            _openFileMap.Remove(filePath);
            _openFiles.Remove(file);
            if (_activeFile == file) {
                ActiveFile = null;
            }
            Session.FileManager.CloseDocument(filePath);
        }

        public async Task<bool> SaveAllAsync() {
            var tasks = _openFiles
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
                    $"Cannot save file: {file.FilePath}\n" +
                    "The file failed to load and may be corrupted. Please close and reopen it.",
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
                GlobalMessageUtil.ShowError($"Failed to save file: {file.FilePath}\n{ex.Message}");
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
                    $"Cannot save file: {file.FilePath}\n" +
                    "The file failed to load and may be corrupted. Please close and reopen it.",
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
                GlobalMessageUtil.ShowError($"Failed to save file: {newPath}\n{ex.Message}");
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
                Session.PreviewServer.Stop();
                ArcLog.GetLogger<WebEditorViewModel>().Info("Debug cancelled");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error($"Debug failed: {ex.Message}");
            }
        }

        private async Task DebugProjectAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await SaveAllAsync()) {
                GlobalMessageUtil.ShowError("Failed to save all files. Please check for errors and try again.");
                ArcLog.GetLogger<WebEditorViewModel>().Error("Debug: failed to save project files");
                return;
            }

            var projectDir = Session.DesignFileUtil.ProjectFolder;
            if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir)) {
                GlobalMessageUtil.ShowError("Project folder not found. Please check your project settings.");
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
                // 注意：不需要把 wp_metadata_basic.json 写入项目目录。
                // GetPlayerStartArgsAsync 只序列化内存中的 basicData 生成启动参数；
                // 预览的运行态数据（effect 文件、runtime 元数据）由核心写入 TempDir。
                _debugJsonString = await _wpControlClient.GetPlayerStartArgsAsync(
                    basicData, RuntimeType.RWeb, null, cancellationToken);

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

            // 清理调试时写入项目目录的临时元数据（不纳入项目管理）
            var basicDataPath = Path.Combine(Session.DesignFileUtil.ProjectFolder, "wp_metadata_basic.json");
            try {
                if (File.Exists(basicDataPath)) File.Delete(basicDataPath);
            }
            catch { /* 忽略清理失败 */ }
        }

        public void CleanupSessions() {
            Session.PreviewServer.Stop();
            CleanupDebugSession();
        }

        #endregion

        private WebEditorFile? _activeFile;
        private readonly List<WebEditorFile> _openFiles = [];
        private readonly Dictionary<string, WebEditorFile> _openFileMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly IUserSettingsClient _userSettings;
        private readonly IWallpaperControlClient _wpControlClient;
        private string? _debugJsonString;
        private string? _previewUrl;
        private PreviewWithWeb? _previewWindow;
        private readonly ArcPageContextKey _contextKey;
    }
}
