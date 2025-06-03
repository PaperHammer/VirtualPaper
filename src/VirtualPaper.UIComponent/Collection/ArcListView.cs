using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VirtualPaper.UIComponent.Collection {
    public partial class ArcListView : ListView {
        ~ArcListView() {
            UnregisterPropertyChangedCallback(ItemsSourceProperty, _itemsSourceChangedToken);
        }

        // Listview 在取消选择项时，会优先将 Selecteditem(Twoway) 设置为 null，再触发控件内事件 SelectionChanged，最后出发外部绑定的 SelectionChanged 事件。
        public bool CancelSelectionEnable {
            get { return (bool)GetValue(CancelSelectionEnableProperty); }
            set { SetValue(CancelSelectionEnableProperty, value); }
        }
        public static readonly DependencyProperty CancelSelectionEnableProperty =
            DependencyProperty.Register(nameof(CancelSelectionEnable), typeof(bool), typeof(ArcListView), new PropertyMetadata(true));

        public ArcListView() {
            DefaultStyleKey = typeof(ListView);
            this.SelectionChanged += ArcListView_SelectionChanged;

            _itemsSourceChangedToken = RegisterPropertyChangedCallback(
                ItemsSourceProperty,
                OnItemsSourcePropertyChanged);        
        }

        private void ArcListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (CancelSelectionEnable) return;

            // 如果没有新的选中项，恢复到上一个选中项或选中第一个元素
            if (e.AddedItems == null || e.AddedItems.Count == 0) {
                if (_lastSelectedItem != null && Items.Contains(_lastSelectedItem)) {
                    SelectedItem = _lastSelectedItem; // 恢复到上一个选中项
                }
                else if (Items.Count > 0) {
                    SelectedItem = Items[0]; // 选中第一个元素
                }
                return;
            }

            // 更新上一个选中项
            _lastSelectedItem = e.AddedItems[0];
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Remove) {
                // 如果删除了选中的元素，选中第一个元素
                if (SelectedItem == null && Items.Count > 0) {
                    SelectedItem = Items[0];
                }
            }
        }

        private void OnItemsSourcePropertyChanged(DependencyObject sender, DependencyProperty dp) {
            if (dp != ItemsSourceProperty)
                return;

            var oldValue = (sender as ArcListView)?.ItemsSource;
            var newValue = ItemsSource;

            if (oldValue is INotifyCollectionChanged oldCollection) {
                oldCollection.CollectionChanged -= OnCollectionChanged;
            }
            if (newValue is INotifyCollectionChanged newCollection) {
                newCollection.CollectionChanged += OnCollectionChanged;
            }

            TrySelectFirstItem(newValue);
        }

        private void TrySelectFirstItem(object newItemsSource) {
            if (SelectedItem != null)
                return;

            switch (newItemsSource) {
                case null:
                    return;

                case IList list when list.Count > 0:
                    SelectedItem = list[0];
                    return;

                case IEnumerable enumerable:
                    try {
                        var firstItem = enumerable.OfType<object>().FirstOrDefault();
                        if (firstItem != null) {
                            SelectedItem = firstItem;
                        }
                    }
                    catch (InvalidOperationException) { }
                    return;
            }
        }

        private object _lastSelectedItem;
        private readonly long _itemsSourceChangedToken;
    }
}
