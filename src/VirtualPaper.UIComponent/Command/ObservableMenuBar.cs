using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VirtualPaper.UIComponent.Command {
    public partial class ObservableMenuBar : MenuBar {
        public IList<MenuBarItem> MiddleItems {
            get { return (IList<MenuBarItem>)GetValue(MiddleItemsProperty); }
            set { SetValue(MiddleItemsProperty, value); }
        }
        public static readonly DependencyProperty MiddleItemsProperty =
            DependencyProperty.Register("MiddleItems", typeof(IList<MenuBarItem>), typeof(ObservableMenuBar), new PropertyMetadata(new(), SetMiddleItems));

        public IList<MenuBarItem> KeepToLeft {
            get { return (IList<MenuBarItem>)GetValue(KeepToLeftProperty); }
            set { SetValue(KeepToLeftProperty, value); }
        }
        public static readonly DependencyProperty KeepToLeftProperty =
            DependencyProperty.Register("KeepToLeft", typeof(IList<MenuBarItem>), typeof(ObservableMenuBar), new PropertyMetadata(new(), SetLeftItems));

        public IList<MenuBarItem> KeepToRight {
            get { return (IList<MenuBarItem>)GetValue(KeepToRightProperty); }
            set { SetValue(KeepToRightProperty, value); }
        }
        public static readonly DependencyProperty KeepToRightProperty =
            DependencyProperty.Register("KeepToRight", typeof(IList<MenuBarItem>), typeof(ObservableMenuBar), new PropertyMetadata(new(), SetRightItems));

        private static void SetMiddleItems(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var instance = d as ObservableMenuBar;
            for (int i = instance.KeepToLeft.Count; i < (e.OldValue as IList<object>)?.Count; i++) {
                instance.Items.RemoveAt(i);
            }
            InsertItems(instance, instance.KeepToLeft.Count, e.NewValue as IList<MenuBarItem>);
        }

        private static void SetRightItems(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var instance = d as ObservableMenuBar;
            AddItems(instance, e.NewValue as IList<MenuBarItem>);
        }

        private static void SetLeftItems(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var instance = d as ObservableMenuBar;
            InsertItems(instance, 0, e.NewValue as IList<MenuBarItem>);
        }

        private static void InsertItems(ObservableMenuBar menuBar, int idx, IList<MenuBarItem> items) {
            if (menuBar == null || items == null) return;

            for (int i = items.Count - 1; i >= 0; i--) {
                menuBar.Items.Insert(idx, items[i]);
            }
        }

        private static void AddItems(ObservableMenuBar menuBar, IList<MenuBarItem> items) {
            if (menuBar == null || items == null) return;

            foreach (var item in items) {
                menuBar.Items.Add(item);
            }
        }
    }

    public partial class KeepToLeft : List<MenuBarItem> { }
    public partial class KeepToRight : List<MenuBarItem> { }
}
