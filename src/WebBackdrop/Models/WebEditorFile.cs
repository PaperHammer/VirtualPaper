using System;
using System.IO;
using VirtualPaper.Models.Mvvm;

namespace Workloads.Creation.WebBackdrop.Models {
    public partial class WebEditorFile : ObservableObject {
        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);
        public string FileExtension => Path.GetExtension(FilePath).ToLowerInvariant();

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
            _content = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
            _isSaved = true;
        }

        public void MarkAsSaved() {
            _isSaved = true;
            OnPropertyChanged(nameof(IsSaved));
        }

        private string _content;
        private bool _isSaved;
    }
}
