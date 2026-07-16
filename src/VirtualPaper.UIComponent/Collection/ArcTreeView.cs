using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VirtualPaper.UIComponent.Collection {
    public partial class ArcTreeView : TreeView {
        public bool CancelSelectionEnable {
            get => (bool)GetValue(CancelSelectionEnableProperty);
            set => SetValue(CancelSelectionEnableProperty, value);
        }
        public static readonly DependencyProperty CancelSelectionEnableProperty =
            DependencyProperty.Register(nameof(CancelSelectionEnable), typeof(bool), typeof(ArcTreeView), new PropertyMetadata(true));

        public ArcTreeView() {
            SelectionChanged += ArcTreeView_SelectionChanged;
        }

        // TreeViewList 行为与 Listview 不同。
        // 在取消选择项时，不会优先将 Selecteditem(Twoway) 设置为 null。而是先触发控件内事件 SelectionChanged
        private void ArcTreeView_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args) {
            if (args.AddedItems.Count > 0) _lastSelectedItem = args.AddedItems[0];

            if (_isRestoringSelection || CancelSelectionEnable) return;

            _isRestoringSelection = true;
            if (SelectedItem == null && _lastSelectedItem != null) {
                SelectedItem = _lastSelectedItem;
            }
            _isRestoringSelection = false;
        }

        private object? _lastSelectedItem;
        private bool _isRestoringSelection;
    }
}
