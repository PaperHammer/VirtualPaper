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

        public double ItemMinHeight {
            get => (double)GetValue(ItemMinHeightProperty);
            set => SetValue(ItemMinHeightProperty, value);
        }
        public static readonly DependencyProperty ItemMinHeightProperty =
            DependencyProperty.Register(nameof(ItemMinHeight), typeof(double), typeof(ArcTreeView), new PropertyMetadata(double.NaN, OnItemLayoutPropertyChanged));

        public double ItemContentHeight {
            get => (double)GetValue(ItemContentHeightProperty);
            set => SetValue(ItemContentHeightProperty, value);
        }
        public static readonly DependencyProperty ItemContentHeightProperty =
            DependencyProperty.Register(nameof(ItemContentHeight), typeof(double), typeof(ArcTreeView), new PropertyMetadata(double.NaN, OnItemLayoutPropertyChanged));

        public Thickness ItemPresenterPadding {
            get => (Thickness)GetValue(ItemPresenterPaddingProperty);
            set => SetValue(ItemPresenterPaddingProperty, value);
        }
        public static readonly DependencyProperty ItemPresenterPaddingProperty =
            DependencyProperty.Register(nameof(ItemPresenterPadding), typeof(Thickness), typeof(ArcTreeView), new PropertyMetadata(default(Thickness), OnItemLayoutPropertyChanged));

        public Thickness ItemPresenterMargin {
            get => (Thickness)GetValue(ItemPresenterMarginProperty);
            set => SetValue(ItemPresenterMarginProperty, value);
        }
        public static readonly DependencyProperty ItemPresenterMarginProperty =
            DependencyProperty.Register(nameof(ItemPresenterMargin), typeof(Thickness), typeof(ArcTreeView), new PropertyMetadata(default(Thickness), OnItemLayoutPropertyChanged));

        public ArcTreeView() {
            SelectionChanged += ArcTreeView_SelectionChanged;
        }

        protected override void OnApplyTemplate() {
            base.OnApplyTemplate();
            UpdateItemLayoutResources();
        }

        private static void OnItemLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcTreeView treeView) {
                treeView.UpdateItemLayoutResources();
            }
        }

        private void UpdateItemLayoutResources() {
            if (!double.IsNaN(ItemMinHeight)) {
                Resources["TreeViewItemMinHeight"] = ItemMinHeight;
            }
            if (!double.IsNaN(ItemContentHeight)) {
                Resources["TreeViewItemContentHeight"] = ItemContentHeight;
            }
            Resources["TreeViewItemPresenterPadding"] = ItemPresenterPadding;
            Resources["TreeViewItemPresenterMargin"] = ItemPresenterMargin;
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
