using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using Windows.Foundation;

namespace VirtualPaper.UIComponent.Container {
    public partial class SelectableItemsControl : ItemsControl {
        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;

        public object SelectedItem {
            get => GetValue(SelectedItemProperty);
            set {
                if (value == SelectedItem) return;

                SelectionChanged?.Invoke(this, new(SelectedItem, value));
                SetValue(SelectedItemProperty, value);
            }
        }
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(SelectableItemsControl),
                new PropertyMetadata(null));

        public SelectableItemsControl() {
            this.SelectionChanged += SelectableItemsControl_SelectionChanged;
        }

        private void SelectableItemsControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItem != null) {
                CustomContainer item = (sender as SelectableItemsControl).ContainerFromItem(e.AddedItem) as CustomContainer;
                if (item != null) {
                    item.IsSelected = true;
                }
            }

            if (e.RemovedItem != null) {
                CustomContainer item = (sender as SelectableItemsControl).ContainerFromItem(e.RemovedItem) as CustomContainer;
                if (item != null) {
                    item.IsSelected = false;
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride() {
            // 返回自定义的容器类型
            return new CustomContainer();
        }

        protected override bool IsItemItsOwnContainerOverride(object item) {
            // 判断是否已经是自定义的容器类型
            return item is CustomContainer;
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item) {
            base.PrepareContainerForItemOverride(element, item);

            if (element is FrameworkElement frameworkElement) {
                frameworkElement.PointerPressed += OnPointerPressed;
                frameworkElement.PointerMoved += OnPointerMoved;
                frameworkElement.PointerReleased += OnPointerReleased;
            }
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e) {
            if (sender is FrameworkElement fe && fe.DataContext != null) {               
                SelectedItem = fe.DataContext;

                draggedItem = fe;
                dragStartPosition = e.GetCurrentPoint(draggedItem).Position;
                // 捕获指针以便接收后续的 PointerMoved 和 PointerReleased 事件
                _ = draggedItem.CapturePointer(e.Pointer);
            }
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e) {
            if (draggedItem != null) {
                var currentPosition = e.GetCurrentPoint(draggedItem).Position;
                if (Math.Abs(currentPosition.X - dragStartPosition.X) > 5 || Math.Abs(currentPosition.Y - dragStartPosition.Y) > 5) {
                    // 开始拖动
                    if (this.IsHitTestVisible) {
                        // 如果鼠标位于 SelectableItemsControl 内，则应用高亮状态
                        VisualStateManager.GoToState(this, nameof(VisualStates.DragOver), false);
                    }
                    else {
                        // 否则移除高亮状态
                        VisualStateManager.GoToState(this, nameof(VisualStates.Normal), false);
                    }
                }
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e) {
            if (draggedItem != null) {
                VisualStateManager.GoToState(this, nameof(VisualStates.Normal), false);
                // 结束拖动
                draggedItem.ReleasePointerCaptures();
                draggedItem = null;

                // 更新数据源中的项目顺序
                UpdateItemsOrder();
            }
        }

        private void UpdateItemsOrder() {
            // 根据项目的当前位置更新数据源中的顺序
            // 这里需要根据实际的布局和需求来计算新位置
            // 简化示例中省略了具体的实现
        }

        private UIElement draggedItem = null;
        private Point dragStartPosition;
    }

    public class SelectionChangedEventArgs(object removedItem, object addedItem) : EventArgs {
        public object RemovedItem { get; init; } = removedItem;
        public object AddedItem { get; init; } = addedItem;
    }

    public partial class CustomContainer : ContentControl {
        private bool _isSelected;
        internal bool IsSelected {
            get { return _isSelected; }
            set { _isSelected = value; VisualStateManager.GoToState(this, value ? nameof(VisualStates.Selected) : nameof(VisualStates.Normal), false); }
        }

        public CustomContainer() {
            DefaultStyleKey = typeof(CustomContainer);

            this.PointerEntered += OnPointerEntered;
            this.PointerExited += OnPointerExited;
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e) {
            VisualStateManager.GoToState((CustomContainer)sender, IsSelected ? nameof(VisualStates.Selected) : nameof(VisualStates.Normal), false);
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e) {
            VisualStateManager.GoToState((CustomContainer)sender, nameof(VisualStates.PointerOver), false);
        }
    }
}
