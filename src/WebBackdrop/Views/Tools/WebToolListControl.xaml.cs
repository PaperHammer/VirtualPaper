using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Workloads.Creation.WebBackdrop.Models;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebToolListControl : UserControl {
        public event EventHandler<RoutedEventArgs>? ToolListLoaded;

        public WebToolItem SelectedTool {
            get => (WebToolItem)GetValue(SelectedToolProperty);
            set => SetValue(SelectedToolProperty, value);
        }
        public static readonly DependencyProperty SelectedToolProperty =
            DependencyProperty.Register(nameof(SelectedTool), typeof(WebToolItem), typeof(WebToolListControl), new PropertyMetadata(null));

        public List<WebToolItem> ToolItems {
            get => (List<WebToolItem>)GetValue(ToolItemsProperty);
            set => SetValue(ToolItemsProperty, value);
        }
        public static readonly DependencyProperty ToolItemsProperty =
            DependencyProperty.Register(nameof(ToolItems), typeof(List<WebToolItem>), typeof(WebToolListControl), new PropertyMetadata(null));

        public WebToolListControl() {
            InitializeComponent();
        }

        private void ArcListView_Loaded(object sender, RoutedEventArgs e) {
            ToolListLoaded?.Invoke(sender, e);
        }
    }
}
