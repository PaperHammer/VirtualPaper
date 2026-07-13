using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebStatusBarControl : UserControl {
        public event EventHandler<RoutedEventArgs>? PreviewRequested;
        public event EventHandler<string>? PanelRequested;
        public event EventHandler<RoutedEventArgs>? RunRequested;
        public event EventHandler<RoutedEventArgs>? DebugRequested;
        public event EventHandler<RoutedEventArgs>? ToggleLeftSideBarRequested;
        public event EventHandler<RoutedEventArgs>? ToggleBottomPanelRequested;
        public event EventHandler<RoutedEventArgs>? ToggleRightSideBarRequested;

        public bool IsLeftSideBarVisible { get; set; } = true;
        public bool IsBottomPanelVisible { get; set; }
        public bool IsRightSideBarVisible { get; set; } = true;

        public Visibility LeftSideBarFillVisibility => IsLeftSideBarVisible ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BottomPanelFillVisibility => IsBottomPanelVisible ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RightSideBarFillVisibility => IsRightSideBarVisible ? Visibility.Visible : Visibility.Collapsed;
        public Visibility LeftSideBarLineVisibility => IsLeftSideBarVisible ? Visibility.Collapsed : Visibility.Visible;
        public Visibility BottomPanelLineVisibility => IsBottomPanelVisible ? Visibility.Collapsed : Visibility.Visible;
        public Visibility RightSideBarLineVisibility => IsRightSideBarVisible ? Visibility.Collapsed : Visibility.Visible;

        public string ActiveFileLanguage {
            get => (string)GetValue(ActiveFileLanguageProperty);
            set => SetValue(ActiveFileLanguageProperty, value);
        }
        public static readonly DependencyProperty ActiveFileLanguageProperty =
            DependencyProperty.Register(nameof(ActiveFileLanguage), typeof(string), typeof(WebStatusBarControl), new PropertyMetadata(string.Empty, OnActiveFileLanguageChanged));

        public int LineNumber {
            get => (int)GetValue(LineNumberProperty);
            set => SetValue(LineNumberProperty, value);
        }
        public static readonly DependencyProperty LineNumberProperty =
            DependencyProperty.Register(nameof(LineNumber), typeof(int), typeof(WebStatusBarControl), new PropertyMetadata(1, OnCursorPositionChanged));

        public int Column {
            get => (int)GetValue(ColumnProperty);
            set => SetValue(ColumnProperty, value);
        }
        public static readonly DependencyProperty ColumnProperty =
            DependencyProperty.Register(nameof(Column), typeof(int), typeof(WebStatusBarControl), new PropertyMetadata(1, OnCursorPositionChanged));

        public string LineText => $"Ln {LineNumber}";
        public string ColumnText => $"Col {Column}";
        public string IndentText => "Spaces: 2";
        public string EncodingText => "UTF-8";
        public string LineEndingText => "CRLF";
        public string LanguageStatusText => $"{{ }} {FormatLanguage(ActiveFileLanguage)}";

        public WebStatusBarControl() {
            InitializeComponent();
        }

        public void RefreshLayoutState() {
            Bindings.Update();
        }

        private static void OnCursorPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is WebStatusBarControl control) {
                control.Bindings.Update();
            }
        }

        private static void OnActiveFileLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is WebStatusBarControl control) {
                control.Bindings.Update();
            }
        }

        private static string FormatLanguage(string language) {
            return language switch {
                "html" => "HTML",
                "css" => "CSS",
                "javascript" => "JavaScript",
                "typescript" => "TypeScript",
                "json" => "JSON",
                "xml" => "XML",
                "markdown" => "Markdown",
                _ => "Plain Text",
            };
        }

        private void Problems_Click(object sender, RoutedEventArgs e) {
            PanelRequested?.Invoke(this, "PROBLEMS");
        }

        private void Run_Click(object sender, RoutedEventArgs e) {
            RunRequested?.Invoke(this, e);
        }

        private void Debug_Click(object sender, RoutedEventArgs e) {
            DebugRequested?.Invoke(this, e);
        }

        private void ToggleLeftSideBar_Click(object sender, RoutedEventArgs e) {
            ToggleLeftSideBarRequested?.Invoke(this, e);
        }

        private void ToggleBottomPanel_Click(object sender, RoutedEventArgs e) {
            ToggleBottomPanelRequested?.Invoke(this, e);
        }

        private void ToggleRightSideBar_Click(object sender, RoutedEventArgs e) {
            ToggleRightSideBarRequested?.Invoke(this, e);
        }
    }
}
