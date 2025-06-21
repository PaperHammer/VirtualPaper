using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace VirtualPaper.UIComponent.Collection {
    public partial class ArcListView : ListView {
        public event EventHandler ItemsMoved;

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

        public bool IsAllwaysSeletedNewItem {
            get { return (bool)GetValue(IsAllwaysSeletedNewItemProperty); }
            set { SetValue(IsAllwaysSeletedNewItemProperty, value); }
        }
        public static readonly DependencyProperty IsAllwaysSeletedNewItemProperty =
            DependencyProperty.Register(nameof(IsAllwaysSeletedNewItem), typeof(bool), typeof(ArcListView), new PropertyMetadata(true));

        public ArcListView() {
            DefaultStyleKey = typeof(ListView);
            this.SelectionChanged += ArcListView_SelectionChanged;
            this.DragItemsStarting += ArcListView_DragItemsStarting;
            this.DragItemsCompleted += ArcListView_DragItemsCompleted;

            _itemsSourceChangedToken = RegisterPropertyChangedCallback(ItemsSourceProperty, OnItemsSourcePropertyChanged);
        }

        // 拖动时，不触发 Remove 与 Add 事件通知外部，避免多次渲染
        private void ArcListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            if (args.DropResult == DataPackageOperation.Move) {
                var newIndex = (this.ItemsSource as IList)?.IndexOf(args.Items[0]) ?? -1;

                if (_oldIndex != -1 && newIndex != -1 && _oldIndex != newIndex) {
                    ItemsMoved?.Invoke(this, EventArgs.Empty);
                }
            }
            _isDragging = false;            
        }

        private void ArcListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e) {
            _isDragging = true;
            _oldIndex = (this.ItemsSource as IList)?.IndexOf(e.Items[0]) ?? -1;
        }

        private void ArcListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            TryPreventNullSelectOnSelectionChanged(e);
        }

        private void TryPreventNullSelectOnSelectionChanged(SelectionChangedEventArgs e) {
            if (e.AddedItems.Count > 0) _lastSelectedItem = e.AddedItems[0];
            
            if (_isDragging || CancelSelectionEnable || SelectedItem != null) return;

            if (_lastSelectedItem != null && Items.Contains(_lastSelectedItem)) {
                SelectedItem = _lastSelectedItem;
            }
            else {
                SelectedItem = Items.FirstOrDefault(); // 如果删除的是被选择项则 ui 无法显示出第一项的视觉效果，需要刷新一次
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    TrySelectNewestItem(e);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    TryPreventNullSelectOnCollectionChanged();
                    break;
                case NotifyCollectionChangedAction.Replace:
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Reset:
                    break;
                default:
                    break;
            }
        }

        private void TryPreventNullSelectOnCollectionChanged() {
            if (_isDragging || CancelSelectionEnable) return;

            SelectedItem = null; // 刷新一次 ui 使得删除的项不再被选中，让 ui 显示被选中项的视觉效果
        }

        private void TrySelectNewestItem(NotifyCollectionChangedEventArgs e) {
            if (_isDragging || !IsAllwaysSeletedNewItem) return;

            SelectedItem = e.NewItems?[0];
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

        /// <summary>
        /// 用于还原选择项，防止空选择
        /// </summary>
        private object _lastSelectedItem;
        private bool _isDragging;
        private int _oldIndex;
        private readonly long _itemsSourceChangedToken;
    }
}
