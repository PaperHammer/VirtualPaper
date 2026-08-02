using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;

namespace Workloads.Creation.WebBackdrop.ViewModels {
    public partial class WebEditorViewModel : ObservableObject {
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

                Session.FileManager.NotifySaved(file.FilePath);
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

        private WebEditorFile? _activeFile;
        private readonly List<WebEditorFile> _openFiles = [];
        private readonly Dictionary<string, WebEditorFile> _openFileMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly IUserSettingsClient _userSettings;
    }
}
