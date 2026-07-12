using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebStatusBarControl : UserControl {
        public event EventHandler<RoutedEventArgs>? PreviewRequested;

        public string ActiveFilePath {
            get => (string)GetValue(ActiveFilePathProperty);
            set => SetValue(ActiveFilePathProperty, value);
        }
        public static readonly DependencyProperty ActiveFilePathProperty =
            DependencyProperty.Register(nameof(ActiveFilePath), typeof(string), typeof(WebStatusBarControl), new PropertyMetadata(string.Empty));

        public string ActiveFileLanguage {
            get => (string)GetValue(ActiveFileLanguageProperty);
            set => SetValue(ActiveFileLanguageProperty, value);
        }
        public static readonly DependencyProperty ActiveFileLanguageProperty =
            DependencyProperty.Register(nameof(ActiveFileLanguage), typeof(string), typeof(WebStatusBarControl), new PropertyMetadata(string.Empty));

        public WebStatusBarControl() {
            InitializeComponent();
        }

        private void Preview_Click(object sender, RoutedEventArgs e) {
            PreviewRequested?.Invoke(this, e);
        }
    }
}
