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

        private WebFileItem? _selectedFileItem;
        public WebFileItem? SelectedFileItem {
            get { return _selectedFileItem; }
            set { if (_selectedFileItem == value) return; _selectedFileItem = value; OnPropertyChanged(); }
        }

        public WebProjectSession Session { get; }

        public readonly List<WebToolItem> ToolItems = [
            new() { Type = WebToolType.FileTree,    ToolName = "Project_WebBackdrop_ToolName_FileTree",    Glyph = "\uE8B7" },
            new() { Type = WebToolType.ProjectInfo, ToolName = "Project_WebBackdrop_ToolName_ProjectInfo", Glyph = "\uE946" },
        ];

        public WebEditorViewModel(WebProjectSession session) {
            Session = session;
            _userSettings = AppServiceLocator.Services.GetRequiredService<IUserSettingsClient>();
            _wpControlClient = AppServiceLocator.Services.GetRequiredService<IWallpaperControlClient>();
        }

        public async Task OpenFileAsync(string filePath) {
            if (_openFileMap.TryGetValue(filePath, out var existing)) {
                ActiveFile = existing;
                Session.FileManager.UpdateSnapshot(filePath);
                return;
            }

            try {
                var file = await WebEditorFile.LoadAsync(filePath);
                _openFiles.Add(file);
                _openFileMap[filePath] = file;
                ActiveFile = file;
                Session.FileManager.UpdateSnapshot(filePath);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error(ex);
                GlobalMessageUtil.ShowError($"Failed to open file: {filePath}\nThe file may be corrupted or unreadable.\n{ex.Message}");
            }
        }

        public void OpenFile(string filePath) {
            if (_openFileMap.TryGetValue(filePath, out var existing)) {
                ActiveFile = existing;
                Session.FileManager.UpdateSnapshot(filePath);
                return;
            }

            try {
                var file = new WebEditorFile(filePath);
                _openFiles.Add(file);
                _openFileMap[filePath] = file;
                ActiveFile = file;
                Session.FileManager.UpdateSnapshot(filePath);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error(ex);
                GlobalMessageUtil.ShowError($"Failed to open file: {filePath}\nThe file may be corrupted or unreadable.\n{ex.Message}");
            }
        }

        public void CloseFile(WebEditorFile file) {
            var idx = _openFiles.IndexOf(file);
            if (idx < 0) return;

            _openFiles.RemoveAt(idx);
            _openFileMap.Remove(file.FilePath);
            Session.FileManager.CloseDocument(file.FilePath);

            if (ActiveFile == file) {
                ActiveFile = _openFiles.Count > 0
                    ? _openFiles[Math.Max(0, idx - 1)]
                    : null;
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

        public async Task<bool> SaveAllAsync() {
            var tasks = _openFiles
                .Where(file => !file.IsSaved)
                .Select(SaveFileAsync);
            var results = await Task.WhenAll(tasks);
            return results.All(result => result);
        }

        private async Task<bool> SaveFileAsync(WebEditorFile file) {
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

                await File.WriteAllTextAsync(file.FilePath, text, enc);
                file.MarkAsSaved();

                // Refresh the disk stamp so the FileSystemWatcher won't treat
                // our own save as an external file change.
                Session.FileManager.NotifySaved(file.FilePath);

                // Trigger hot reload for active debug session
                SyncFileChange(file.FilePath);

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error(ex);
                return false;
            }
        }

        public async Task UpdateRecentUsedAsync(string filePath) {
            if (!string.IsNullOrEmpty(filePath))
                await _userSettings.UpdateRecentUsedAsync(filePath);
        }

        #region Run / Debug

        public async void DebugProject() {
            SaveAllFiles();

            var projectDir = Session.DesignFileUtil.ProjectFolder;
            if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir)) {
                ArcLog.GetLogger<WebEditorViewModel>().Warn("Debug: project folder not found");
                return;
            }

            try {
                CleanupDebugSession();

                var projectName = Session.DesignFileUtil.ProjectName;
                var debugTempDir = Path.Combine(Path.GetTempPath(), "VirtualPaper", "debug",
                    $"{projectName}_{DateTime.Now:yyyyMMddHHmmss}");
                CopyDirectoryRecursive(projectDir, debugTempDir);
                _debugTempDir = debugTempDir;

                var srcEntry = Session.DesignFileUtil.EntryFilePath;
                var relativeEntry = Path.GetRelativePath(projectDir, srcEntry);
                _debugEntryPath = Path.Combine(debugTempDir, relativeEntry);

                if (File.Exists(_debugEntryPath)) {
                    var wpBasicDataPath = Path.Combine(debugTempDir, "wp_metadata_basic.json");
                    var basicData = new WpBasicData {
                        WallpaperUid = Guid.NewGuid().ToString(),
                        Title = projectName,
                        FilePath = _debugEntryPath,
                        FolderPath = debugTempDir,
                        FolderName = Path.GetFileName(debugTempDir),
                        FType = FileType.FWebZip,
                    };
                    JsonSaver.Save(wpBasicDataPath, basicData, WpBasicDataContext.Default);

                    _debugJsonString = await _wpControlClient.GetPlayerStartArgsAsync(
                        basicData, RuntimeType.RWeb, null, CancellationToken.None);

                    OpenPreviewWindow();
                }

                ArcLog.GetLogger<WebEditorViewModel>().Info($"Debug: temp={debugTempDir}");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error($"Debug failed: {ex.Message}");
            }
        }

        public void SyncFileChange(string srcPath) {
            if (string.IsNullOrEmpty(_debugTempDir)) return;
            var projectDir = Session.DesignFileUtil.ProjectFolder;
            if (string.IsNullOrEmpty(projectDir)) return;

            try {
                var relativePath = Path.GetRelativePath(projectDir, srcPath);
                if (relativePath.StartsWith("..") || Path.IsPathRooted(relativePath)) return;

                var destPath = Path.Combine(_debugTempDir, relativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

                if (File.Exists(srcPath)) {
                    File.Copy(srcPath, destPath, overwrite: true);
                    ReloadPreviewWindow(relativePath);
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error($"Sync failed: {srcPath}: {ex.Message}");
            }
        }

        public void SyncFileDelete(string srcPath) {
            if (string.IsNullOrEmpty(_debugTempDir)) return;
            var projectDir = Session.DesignFileUtil.ProjectFolder;
            if (string.IsNullOrEmpty(projectDir)) return;

            try {
                var relativePath = Path.GetRelativePath(projectDir, srcPath);
                if (relativePath.StartsWith("..") || Path.IsPathRooted(relativePath)) return;

                var destPath = Path.Combine(_debugTempDir, relativePath);
                if (File.Exists(destPath)) File.Delete(destPath);
                if (Directory.Exists(destPath)) Directory.Delete(destPath, recursive: true);

                ReloadPreviewWindow(relativePath);
            }
            catch { /* ignore */ }
        }

        private void OpenPreviewWindow() {
            if (_debugJsonString == null) return;
            var previewWindow = new PreviewWithWeb(_debugJsonString, enableHmr: true);
            previewWindow.Closed += (_, _) => {
                _previewWindow = null;
                DebugSessionEnded?.Invoke();
            };
            previewWindow.Show();
            previewWindow.Activate();
            _previewWindow = previewWindow;
        }

        private void ReloadPreviewWindow(string relativePath) {
            if (_previewWindow == null) {
                ArcLog.GetLogger<WebEditorViewModel>().Warn("ReloadPreviewWindow: _previewWindow is null");
                return;
            }
            try {
                // Use forward-slash for consistent cross-platform path matching
                // in the JS hotreload handlers.
                var normalized = relativePath.Replace('\\', '/');
                _previewWindow.OnFileChanged(normalized);
                ArcLog.GetLogger<WebEditorViewModel>().Info($"Preview reloaded: {normalized}");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error($"Reload failed: {ex.Message}");
            }
        }

        private void SaveAllFiles() {
            _ = SaveAllAsync();
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir) {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir)) {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir)) {
                CopyDirectoryRecursive(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }

        private void CleanupDebugSession() {
            _previewWindow?.Close();
            _previewWindow = null;
            _debugJsonString = null;
            _debugEntryPath = null;

            if (_debugTempDir != null && Directory.Exists(_debugTempDir)) {
                try { Directory.Delete(_debugTempDir, recursive: true); } catch { }
                _debugTempDir = null;
            }
        }

        public void CleanupSessions() {
            CleanupDebugSession();
        }

        #endregion

        private WebEditorFile? _activeFile;
        private readonly List<WebEditorFile> _openFiles = [];
        private readonly Dictionary<string, WebEditorFile> _openFileMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly IUserSettingsClient _userSettings;
        private readonly IWallpaperControlClient _wpControlClient;
        private string? _debugTempDir;
        private string? _debugEntryPath;
        private string? _debugJsonString;
        private PreviewWithWeb? _previewWindow;
    }
}
