using System;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace Workloads.Creation.WebBackdrop.Models {
    public enum WebEditorFileKind {
        Text,
        Markdown,
        Image,
        Unsupported,
    }

    public partial class WebEditorFile : ObservableObject {
        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);
        public string FileExtension => Path.GetExtension(FilePath).ToLowerInvariant();

        public bool CanOpenAsText => Kind is WebEditorFileKind.Text or WebEditorFileKind.Markdown;
        public WebEditorFileKind Kind => GetKind(FileExtension);

        public string Content {
            get => _content;
            set {
                if (_content == value) return;
                _content = value;
                OnPropertyChanged();
                IsSaved = false;
            }
        }

        public bool IsSaved {
            get => _isSaved;
            private set {
                if (_isSaved == value) return;
                _isSaved = value;
                OnPropertyChanged();
                IsSavedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? IsSavedChanged;

        public WebEditorFile(string filePath) {
            FilePath = filePath;
            _content = ReadContent(filePath);
            _isSaved = true;
        }

        public static async Task<WebEditorFile> LoadAsync(string filePath) {
            var content = await ReadContentAsync(filePath);
            return new WebEditorFile(filePath, content);
        }

        private WebEditorFile(string filePath, string content) {
            FilePath = filePath;
            _content = content;
            _isSaved = true;
        }

        public async Task ReloadAsync() {
            _content = await ReadContentAsync(FilePath);
            _isSaved = true;
            OnPropertyChanged(nameof(Content));
            OnPropertyChanged(nameof(IsSaved));
        }

        public void MarkAsSaved() {
            _isSaved = true;
            OnPropertyChanged(nameof(IsSaved));
        }

        private static WebEditorFileKind GetKind(string extension) {
            if (WebEditorFileUtil.IsMarkdownExtension(extension)) return WebEditorFileKind.Markdown;
            if (WebEditorFileUtil.IsPreviewImageExtension(extension)) return WebEditorFileKind.Image;
            if (WebEditorFileUtil.IsTextExtension(extension)) return WebEditorFileKind.Text;
            return WebEditorFileKind.Unsupported;
        }

        private static string ReadContent(string filePath) {
            return File.Exists(filePath) && WebEditorFileUtil.IsTextExtension(Path.GetExtension(filePath))
                ? File.ReadAllText(filePath)
                : string.Empty;
        }

        private static async Task<string> ReadContentAsync(string filePath) {
            return File.Exists(filePath) && WebEditorFileUtil.IsTextExtension(Path.GetExtension(filePath))
                ? await File.ReadAllTextAsync(filePath)
                : string.Empty;
        }

        private string _content;
        private bool _isSaved;
    }
}
