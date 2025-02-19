using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.UI.Input;
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

                SelectionChangedEventArgs eventArgs = new(SelectedItem, value);
                SelectionChanged?.Invoke(this, eventArgs);
                OnSelectionChanged(eventArgs);

                SetValue(SelectedItemProperty, value);
                _contentFrame.DataContext = value;
            }
        }
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(SelectableItemsControl),
                new PropertyMetadata(null));

        private FrameworkElement _pointerOverItem;
        private FrameworkElement PointerOverItem {
            get { return _pointerOverItem; }
            set {
                // todo: 待改进-短时间内无法触发交换
                // 排序后指针指向的元素是上一次交换的元素，则不允许触发重新交换
                if (_pointerOverItem == value) return;
                _pointerOverItem = value;
                _exChangable = true;
            }
        }

        public SelectableItemsControl() {
            this.DefaultStyleKey = typeof(SelectableItemsControl);

            this.Loaded += SelectableItemsControl_Loaded;
            this.Unloaded += SelectableItemsControl_Unloaded;
        }

        private void SelectableItemsControl_Unloaded(object sender, RoutedEventArgs e) {
            foreach (var win in _flyoutItems) {
                win.Close();
            }
        }

        protected override void OnApplyTemplate() {
            _contentFrame = this.GetTemplateChild("ContentFrame") as FrameworkElement;
            _tabHeader = this.GetTemplateChild("TabHeader") as FrameworkElement;
        }

        private void SelectableItemsControl_Loaded(object sender, RoutedEventArgs e) {
            _tabHeader.PointerEntered += TabHeader_PointerEntered;
            _tabHeader.PointerPressed += TabHeader_PointerPressed;
            _tabHeader.PointerReleased += TabHeader_PointerReleased;
            _tabHeader.PointerMoved += TabHeader_PointerMoved;
            _tabHeader.DragEnter += TabHeader_DragEnter;
            _tabHeader.DragLeave += TabHeader_DragLeave;

            this.PointerMoved += SelectableItemsControl_PointerMoved;
            this.PointerExited += SelectableItemsControl_PointerExited;

            SelectedItem = this.Items[0];
        }

        private void TabHeader_DragLeave(object sender, DragEventArgs e) {
            VisualStateManager.GoToState(this, nameof(VisualStates.DragOver), false);
        }

        private void TabHeader_DragEnter(object sender, DragEventArgs e) {
            VisualStateManager.GoToState(this, nameof(VisualStates.Normal), false);
        }

        private void SelectableItemsControl_PointerExited(object sender, PointerRoutedEventArgs e) {
            RemoveAndFlyoutItem(e);
        }

        private void SelectableItemsControl_PointerMoved(object sender, PointerRoutedEventArgs e) {
            RemoveAndFlyoutItem(e);
        }

        private void TabHeader_PointerEntered(object sender, PointerRoutedEventArgs e) {
            if (!_isExChanging && e.OriginalSource is FrameworkElement fe && fe.DataContext != null) {
                PointerOverItem = this.ContainerFromItem(fe.DataContext) as CustomContainer;
            }
        }

        private void TabHeader_PointerPressed(object sender, PointerRoutedEventArgs e) {
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext != null) {
                SelectedItem = fe.DataContext;

                PointerPoint pointerPoint = e.GetCurrentPoint(fe);
                if (pointerPoint.Properties.IsLeftButtonPressed) {
                    _draggedItem = this.ContainerFromItem(fe.DataContext) as CustomContainer;
                    _dragStartPosition = pointerPoint.Position;
                }
            }
        }

        private void TabHeader_PointerReleased(object sender, PointerRoutedEventArgs e) {
            _draggedItem = null;
        }

        private void TabHeader_PointerMoved(object sender, PointerRoutedEventArgs e) {
            if (_draggedItem != null) {
                var posForConatiner = e.GetCurrentPoint(_draggedItem).Position;
                if (_exChangable && StartDrag(posForConatiner) && PointerOverItem.DataContext != _draggedItem.DataContext) {
                    _isExChanging = true;
                    _exChangable = false;
                    int targetIndex = this.Items.IndexOf(PointerOverItem.DataContext);
                    (this.ItemsSource as IList).Remove(_draggedItem.DataContext);
                    (this.ItemsSource as IList).Insert(targetIndex, _draggedItem.DataContext);
                    _isExChanging = false;

                    OnSelectionChanged(new(null, _draggedItem.DataContext)); // 容器 CustomContainer 会重建，导致 IsSelected 状态丢失
                    e.Handled = true;
                }
            }
        }

        private bool StartDrag(Point posForConatiner) {
            return Math.Abs(posForConatiner.X - _dragStartPosition.X) > 5 || Math.Abs(posForConatiner.Y - _dragStartPosition.Y) > 5;
        }

        private void RemoveAndFlyoutItem(PointerRoutedEventArgs e) {
            _exChangable = false;
            if (_draggedItem != null && CheckPointerIsNotInTabHeader(e)) {
                var items = this.ItemsSource as IList;
                items.Remove(_draggedItem.DataContext);
                FlyoutItem(e);
                _draggedItem = null;
                SelectedItem = items.Count > 0 ? items[^1] : null;
            }
            _pointerOverItem = null;
        }

        private void FlyoutItem(PointerRoutedEventArgs e) {
            _draggedItem.CapturePointer(e.Pointer); // 保证焦点在浮动项上

            var flyoutWindow = new FlyoutWindow([_draggedItem], e);
            flyoutWindow.Activate();

            _flyoutItems.Add(flyoutWindow);
        }

        private bool CheckPointerIsNotInTabHeader(PointerRoutedEventArgs e) {
            var pointerPosition = e.GetCurrentPoint(_tabHeader);
            // 检查指针是否还在控件范围内
            return pointerPosition.Position.X < 0 || pointerPosition.Position.X > _tabHeader.ActualWidth ||
               pointerPosition.Position.Y < 0 || pointerPosition.Position.Y > _tabHeader.ActualHeight;
        }


        protected override DependencyObject GetContainerForItemOverride() {
            // 返回自定义的容器类型
            return new CustomContainer();
        }

        protected override bool IsItemItsOwnContainerOverride(object item) {
            // 判断是否已经是自定义的容器类型
            return item is CustomContainer;
        }

        private void OnSelectionChanged(SelectionChangedEventArgs e) {
            if (e.RemovedItem != null && this.ContainerFromItem(e.RemovedItem) is CustomContainer removedCc) {
                removedCc.IsSelected = false;
            }

            if (e.AddedItem != null && this.ContainerFromItem(e.AddedItem) is CustomContainer addedCc) {
                addedCc.IsSelected = true;
            }
        }

        /// <summary>
        /// PointerOverItem 是否改变 => 是否能触发移动
        /// </summary>
        bool _exChangable;

        /// <summary>
        /// 当前是否正在移动 => 阻止 PointerEnter 事件
        /// </summary>
        bool _isExChanging;

        private Point _dragStartPosition;
        private FrameworkElement _draggedItem, _tabHeader, _contentFrame;
        private readonly List<Window> _flyoutItems = [];
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
