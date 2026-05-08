using System;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.IntelligentPanel.Models {
    public class StyleOptionItem : ObservableObject, IEquatable<StyleOptionItem> {
        private string? _name;
        public string? Name {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string? ThumbnailResourceKey { get; set; }

        private string? _imagePath;
        public string? ImagePath {
            get => _imagePath;
            set { _imagePath = value; OnPropertyChanged(); }
        }

        private string? _fileSize;
        public string? FileSize {
            get => _fileSize;
            set { _fileSize = value; OnPropertyChanged(); }
        }

        private string? _fileExt;
        public string? FileExt {
            get => _fileExt;
            set { _fileExt = value; OnPropertyChanged(); }
        }
        public bool IsCustom { get; internal set; }

        public bool Equals(StyleOptionItem? other) {
            return other != null && other.ImagePath == this.ImagePath;
        }

        public override bool Equals(object? obj) {
            return Equals(obj as StyleOptionItem);
        }

        public override int GetHashCode() {
            return ImagePath?.GetHashCode() ?? 0;
        }
    }
}
