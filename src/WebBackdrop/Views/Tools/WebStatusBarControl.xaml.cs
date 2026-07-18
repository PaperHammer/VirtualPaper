using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

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

        public int SelectedCharacterCount {
            get => (int)GetValue(SelectedCharacterCountProperty);
            set => SetValue(SelectedCharacterCountProperty, value);
        }
        public static readonly DependencyProperty SelectedCharacterCountProperty =
            DependencyProperty.Register(nameof(SelectedCharacterCount), typeof(int), typeof(WebStatusBarControl), new PropertyMetadata(0, OnCursorPositionChanged));

        public bool IsSelectedCharacterCountOverflow {
            get => (bool)GetValue(IsSelectedCharacterCountOverflowProperty);
            set => SetValue(IsSelectedCharacterCountOverflowProperty, value);
        }
        public static readonly DependencyProperty IsSelectedCharacterCountOverflowProperty =
            DependencyProperty.Register(nameof(IsSelectedCharacterCountOverflow), typeof(bool), typeof(WebStatusBarControl), new PropertyMetadata(false, OnCursorPositionChanged));

        public int ProblemErrorCount {
            get => (int)GetValue(ProblemErrorCountProperty);
            set => SetValue(ProblemErrorCountProperty, value);
        }
        public static readonly DependencyProperty ProblemErrorCountProperty =
            DependencyProperty.Register(nameof(ProblemErrorCount), typeof(int), typeof(WebStatusBarControl), new PropertyMetadata(0, OnProblemCountChanged));

        public int ProblemWarningCount {
            get => (int)GetValue(ProblemWarningCountProperty);
            set => SetValue(ProblemWarningCountProperty, value);
        }
        public static readonly DependencyProperty ProblemWarningCountProperty =
            DependencyProperty.Register(nameof(ProblemWarningCount), typeof(int), typeof(WebStatusBarControl), new PropertyMetadata(0, OnProblemCountChanged));

        public string CursorPositionText => SelectedCharacterCount > 0
            ? $"Ln {LineNumber}, Col {Column} ({SelectedCharacterCount}{(IsSelectedCharacterCountOverflow ? "+" : string.Empty)} selected)"
            : $"Ln {LineNumber}, Col {Column}";
        public string ProblemErrorText => ProblemErrorCount.ToString();
        public string ProblemWarningText => ProblemWarningCount.ToString();
        public Brush ProblemErrorBrush => ProblemErrorCount > 0
            ? new SolidColorBrush(Color.FromArgb(255, 255, 77, 77))
            : (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];
        public Brush ProblemWarningBrush => ProblemWarningCount > 0
            ? new SolidColorBrush(Colors.Orange)
            : (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];
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

        private static void OnProblemCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
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
