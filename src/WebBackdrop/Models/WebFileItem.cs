using System;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace Workloads.Creation.WebBackdrop.Models {
    public enum WebFileItemType {
        Folder,
        File,
    }

    public partial class WebFileItem : ObservableObject, IEquatable<WebFileItem> {
        public string FilePath { get; }
        public WebFileItemType Type { get; }
        public ObservableCollection<WebFileItem> Children { get; } = [];
        public WebFileItem? Parent { get; }
        public string FileName => Path.GetFileName(FilePath);
        public bool IsChildrenLoaded { get; set; }

        public bool IsSaved {
            get => _isSaved;
            set {
                if (_isSaved == value) return;
                _isSaved = value;
                OnPropertyChanged();
            }
        }

        public bool IsExpanded {
            get => _isExpanded;
            set {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FolderIconSource));
            }
        }

        public BitmapImage? FolderIconSource => Application.Current.Resources.TryGetValue(
            IsExpanded ? "WebBackdrop_FileTree_FolderOpen" : "WebBackdrop_FileTree_Folder", out var resource) && resource is BitmapImage image
            ? image
            : null;

        public BitmapImage? IconSource => Application.Current.Resources.TryGetValue(IconResourceKey, out var resource) && resource is BitmapImage image
            ? image
            : null;

        private string IconResourceKey => WebEditorFileUtil.GetIconResourceKeyFromExtension(Path.GetExtension(FilePath));

        public WebFileItem(string filePath, WebFileItemType type, WebFileItem? parent = null) {
            FilePath = filePath;
            Type = type;
            Parent = parent;
        }

        public bool Equals(WebFileItem? other) {
            if (other == null) return false;
            return FilePath == other.FilePath && Type == other.Type;
        }

        public override bool Equals(object? obj) {
            return obj is WebFileItem other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(FilePath, Type);
        }

        private bool _isSaved = true;
        private bool _isExpanded;
    }
}
