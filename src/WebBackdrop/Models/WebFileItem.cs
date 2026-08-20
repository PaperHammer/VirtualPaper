using System;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
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
        public bool IsPlaceholder { get; init; }

        public bool ExistsOnDisk {
            get => _existsOnDisk;
            set {
                if (_existsOnDisk == value) return;
                _existsOnDisk = value;
                OnPropertyChanged();
            }
        }

        private static SolidColorBrush? _missingBrush;
        /// <summary>
        /// 缺失文件红色笔刷：绑定会频繁求值，共享同一个实例避免每次访问都 new。
        /// </summary>
        public SolidColorBrush MissingBrush => _missingBrush ??= new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 80, 80));

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

        /// <summary>是否处于行内重命名模式（名称区显示为输入框）。</summary>
        public bool IsRenaming {
            get => _isRenaming;
            set {
                if (_isRenaming == value) return;
                _isRenaming = value;
                OnPropertyChanged();
            }
        }

        /// <summary>行内重命名输入框的当前文本。</summary>
        public string RenameText {
            get => _renameText;
            set {
                if (_renameText == value) return;
                _renameText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>重命名输入是否非法（名称非法时展示红框提示）。</summary>
        public bool IsRenameInvalid {
            get => _isRenameInvalid;
            set {
                if (_isRenameInvalid == value) return;
                _isRenameInvalid = value;
                OnPropertyChanged();
            }
        }

        /// <summary>是否通过文件过滤（未命中过滤条件时隐藏）。</summary>
        public bool IsVisible {
            get => _isVisible;
            set {
                if (_isVisible == value) return;
                _isVisible = value;
                OnPropertyChanged();
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
            _existsOnDisk = Type == WebFileItemType.Folder
                ? Directory.Exists(filePath)
                : File.Exists(filePath);
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
        private bool _existsOnDisk;
        private bool _isRenaming;
        private bool _isRenameInvalid;
        private bool _isVisible = true;
        private string _renameText = string.Empty;
    }
}
