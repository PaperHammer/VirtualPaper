using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Views.Tools;

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
                await existing.ReloadAsync();
                ActiveFile = existing;
                return;
            }

            var file = await WebEditorFile.LoadAsync(filePath);
            _openFiles.Add(file);
            _openFileMap[filePath] = file;
            ActiveFile = file;
        }

        public void OpenFile(string filePath) {
            if (_openFileMap.TryGetValue(filePath, out var existing)) {
                ActiveFile = existing;
                return;
            }

            var file = new WebEditorFile(filePath);
            _openFiles.Add(file);
            _openFileMap[filePath] = file;
            ActiveFile = file;
        }

        public void CloseFile(WebEditorFile file) {
            var idx = _openFiles.IndexOf(file);
            if (idx < 0) return;

            _openFiles.RemoveAt(idx);
            _openFileMap.Remove(file.FilePath);

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

        public async Task<bool> SaveAllAsync() {
            bool allOk = true;
            foreach (var f in _openFiles) {
                if (!f.IsSaved)
                    allOk &= await SaveFileAsync(f);
            }
            return allOk;
        }

        private static async Task<bool> SaveFileAsync(WebEditorFile file) {
            try {
                await File.WriteAllTextAsync(file.FilePath, file.Content);
                file.MarkAsSaved();
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
