using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Workloads.Creation.StaticImg.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class ToolListConrtol : UserControl {
        public event EventHandler<RoutedEventArgs> ToolListLoaded;

        public ToolItem SelectedTool {
            get { return (ToolItem)GetValue(SelectedToolProperty); }
            set { SetValue(SelectedToolProperty, value); }
        }
        public static readonly DependencyProperty SelectedToolProperty =
            DependencyProperty.Register(nameof(SelectedTool), typeof(ToolItem), typeof(ToolListConrtol), new PropertyMetadata(null));

        public List<ToolItem> ToolItems {
            get { return (List<ToolItem>)GetValue(ToolItemsProperty); }
            set { SetValue(ToolItemsProperty, value); }
        }
        public static readonly DependencyProperty ToolItemsProperty =
            DependencyProperty.Register(nameof(ToolItems), typeof(List<ToolItem>), typeof(ToolListConrtol), new PropertyMetadata(null));

        public ToolListConrtol() {
            this.InitializeComponent();
        }

        private void ArcListViewToolItem_Loaded(object sender, RoutedEventArgs e) {
            ToolListLoaded?.Invoke(sender, e);
        }
    }
}
