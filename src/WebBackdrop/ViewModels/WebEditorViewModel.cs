using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace Workloads.Creation.WebBackdrop.ViewModels {
    public partial class WebEditorViewModel : ObservableObject {
        public ObservableCollection<WebEditorFile> OpenFiles { get; } = [];

        public WebEditorFile? ActiveFile {
            get => _activeFile;
            set {
                if (_activeFile == value) return;
                _activeFile = value;
                OnPropertyChanged();
            }
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

        public void OpenFile(string filePath) {
            var existing = OpenFiles.FirstOrDefault(f => f.FilePath == filePath);
            if (existing != null) {
                ActiveFile = existing;
                return;
            }

            var file = new WebEditorFile(filePath);
            OpenFiles.Add(file);
            ActiveFile = file;
        }

        public void CloseFile(WebEditorFile file) {
            var idx = OpenFiles.IndexOf(file);
            OpenFiles.Remove(file);

            if (ActiveFile == file) {
                ActiveFile = OpenFiles.Count > 0
                    ? OpenFiles[Math.Max(0, idx - 1)]
                    : null;
            }
        }

        public async Task<bool> SaveActiveFileAsync() {
            if (ActiveFile == null) return false;
            return await SaveFileAsync(ActiveFile);
        }

        public async Task<bool> SaveAllAsync() {
            bool allOk = true;
            foreach (var f in OpenFiles) {
                if (!f.IsSaved)
                    allOk &= await SaveFileAsync(f);
            }
            return allOk;
        }

        private static Task<bool> SaveFileAsync(WebEditorFile file) {
            try {
                File.WriteAllText(file.FilePath, file.Content);
                file.MarkAsSaved();
                return Task.FromResult(true);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebEditorViewModel>().Error(ex);
                return Task.FromResult(false);
            }
        }

        public async Task UpdateRecentUsedAsync(string filePath) {
            if (!string.IsNullOrEmpty(filePath))
                await _userSettings.UpdateRecentUsedAsync(filePath);
        }

        private WebEditorFile? _activeFile;
        private readonly IUserSettingsClient _userSettings;
    }
}
