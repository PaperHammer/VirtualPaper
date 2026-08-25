using System;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
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
                _iconGeometry = null;
                _iconBrush = null;

                OnPropertyChanged();
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(IconGeometry));
                OnPropertyChanged(nameof(IconBrush));
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
                OnPropertyChanged(nameof(FolderIconGeometry));
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

        /// <summary>是否为中间编辑器当前显示的活动文件。</summary>
        public bool IsActiveFile {
            get => _isActiveFile;
            set {
                if (_isActiveFile == value) return;
                _isActiveFile = value;
                OnPropertyChanged();
            }
        }

        public Geometry? FolderIconGeometry =>
            IsExpanded
                ? _folderOpenIconGeometry ??= CreateGeometry("WebBackdrop_FileTree_FolderOpen_Data")
                : _folderIconGeometry ??= CreateGeometry("WebBackdrop_FileTree_Folder_Data");

        public Geometry? IconGeometry =>
            _iconGeometry ??= CreateGeometry($"{IconResourceKey}_Data");

        public Brush? IconBrush =>
            _iconBrush ??= GetResource<Brush>($"{IconResourceKey}_Brush");

        private string IconResourceKey =>
            WebEditorFileUtil.GetIconResourceKeyFromExtension(
                Path.GetExtension(FilePath));

        private static T? GetResource<T>(string resourceKey) where T : class =>
            Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            && resource is T typedResource
                ? typedResource
                : null;

        private static Geometry? CreateGeometry(string resourceKey) =>
            GetResource<string>(resourceKey) is { Length: > 0 } pathData
                ? XamlBindingHelper.ConvertValue(typeof(Geometry), pathData) as Geometry
                : null;

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
        private Geometry? _folderIconGeometry;
        private Geometry? _folderOpenIconGeometry;
        private Geometry? _iconGeometry;
        private Brush? _iconBrush;
        private bool _existsOnDisk;
        private bool _isRenaming;
        private bool _isRenameInvalid;
        private bool _isVisible = true;
        private bool _isActiveFile;

        private string _renameText = string.Empty;
    }
}
