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

        public string FilePath {
            get => _filePath;
            private set {
                if (string.Equals(_filePath, value, StringComparison.OrdinalIgnoreCase)) {
                    return;
                }

                _filePath = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(IconSource));
            }
        }

        public WebFileItemType Type { get; }

        public ObservableCollection<WebFileItem> Children { get; } = [];

        public WebFileItem? Parent {
            get => _parent;
            private set {
                if (ReferenceEquals(_parent, value)) {
                    return;
                }

                _parent = value;
                OnPropertyChanged();
            }
        }

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
        /// 缺失文件红色笔刷。
        /// </summary>
        public SolidColorBrush MissingBrush =>
            _missingBrush ??= new SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 220, 80, 80));

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

        /// <summary>
        /// 是否处于行内重命名模式。
        /// </summary>
        public bool IsRenaming {
            get => _isRenaming;
            set {
                if (_isRenaming == value) return;

                _isRenaming = value;
                OnPropertyChanged();
            }
        }

        public string RenameText {
            get => _renameText;
            set {
                if (_renameText == value) return;

                _renameText = value;
                OnPropertyChanged();
            }
        }

        public bool IsRenameInvalid {
            get => _isRenameInvalid;
            set {
                if (_isRenameInvalid == value) return;

                _isRenameInvalid = value;
                OnPropertyChanged();
            }
        }

        public bool IsVisible {
            get => _isVisible;
            set {
                if (_isVisible == value) return;

                _isVisible = value;
                OnPropertyChanged();
            }
        }

        public BitmapImage? FolderIconSource =>
            Application.Current.Resources.TryGetValue(
                IsExpanded
                    ? "WebBackdrop_FileTree_FolderOpen"
                    : "WebBackdrop_FileTree_Folder",
                out var resource)
            && resource is BitmapImage image
                ? image
                : null;

        public BitmapImage? IconSource =>
            Application.Current.Resources.TryGetValue(
                IconResourceKey,
                out var resource)
            && resource is BitmapImage image
                ? image
                : null;

        private string IconResourceKey =>
            WebEditorFileUtil.GetIconResourceKeyFromExtension(
                Path.GetExtension(FilePath));

        public WebFileItem(
            string filePath,
            WebFileItemType type,
            WebFileItem? parent = null) {

            _filePath = filePath;
            Type = type;
            _parent = parent;

            _existsOnDisk =
                Type == WebFileItemType.Folder
                    ? Directory.Exists(filePath)
                    : File.Exists(filePath);
        }

        /// <summary>
        /// 更新当前节点的位置
        ///
        /// 注意：
        /// 这个方法不会修改 Children 集合
        /// 已加载的子节点由 VM 根据新的父路径一起重绑定
        /// </summary>
        internal void RebindLocation(string newPath, WebFileItem? newParent) {
            FilePath = newPath;
            Parent = newParent;
        }

        public bool Equals(WebFileItem? other) {
            if (other == null) {
                return false;
            }

            return Type == other.Type && string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj) {
            return obj is WebFileItem other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath),
                Type);
        }

        private string _filePath;
        private WebFileItem? _parent;

        private bool _isSaved = true;
        private bool _isExpanded;
        private bool _existsOnDisk;
        private bool _isRenaming;
        private bool _isRenameInvalid;
        private bool _isVisible = true;

        private string _renameText = string.Empty;
    }
}
