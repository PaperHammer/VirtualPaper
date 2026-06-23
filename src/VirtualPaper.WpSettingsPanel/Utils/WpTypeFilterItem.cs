using System.Collections.Generic;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.WpSettingsPanel.Utils {
    /// <summary>
    /// 类型过滤栏中的单个可勾选项。
    /// 一个 item 可映射多个 <see cref="FileType"/>（如"动态图像"→GIF+AI图像）。
    /// </summary>
    public sealed partial class WpTypeFilterItem : ObservableObject {
        /// <summary>该选项代表的文件类型集合。</summary>
        public IReadOnlyList<FileType> FTypes { get; }

        /// <summary>显示文本（i18n 已在 ViewModel 层完成）。</summary>
        public string Label { get; }

        // 使用 bool? 与 CheckBox.IsChecked（也是 Nullable<bool>）类型完全匹配，
        // 确保 x:Bind TwoWay 在选中/取消两个方向均能正常写回。
        private bool? _isSelected = false;
        public bool? IsSelected {
            get => _isSelected;
            set {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public WpTypeFilterItem(IEnumerable<FileType> fTypes, string label) {
            FTypes = [.. fTypes];
            Label = label;
        }
    }
}
