using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Views.Components;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebStatusBarControl : UserControl {
        public event EventHandler<WebEditorBottomPanel>? PanelRequested;
        public event EventHandler<RoutedEventArgs>? RunRequested;
        public event EventHandler<RoutedEventArgs>? ToggleLeftSideBarRequested;
        public event EventHandler<RoutedEventArgs>? ToggleBottomPanelRequested;
        public event EventHandler<RoutedEventArgs>? ToggleRightSideBarRequested;
        public event EventHandler<(int TabSize, bool InsertSpaces)>? IndentChanged;
        public event EventHandler<string>? EncodingChanged;
        public event EventHandler<string>? LineEndingChanged;

        public bool IsLeftSideBarVisible {
            get => (bool)GetValue(IsLeftSideBarVisibleProperty);
            set => SetValue(IsLeftSideBarVisibleProperty, value);
        }
        public static readonly DependencyProperty IsLeftSideBarVisibleProperty =
            DependencyProperty.Register(nameof(IsLeftSideBarVisible), typeof(bool), typeof(WebStatusBarControl),
                new PropertyMetadata(true));

        public bool IsBottomPanelVisible {
            get => (bool)GetValue(IsBottomPanelVisibleProperty);
            set => SetValue(IsBottomPanelVisibleProperty, value);
        }
        public static readonly DependencyProperty IsBottomPanelVisibleProperty =
            DependencyProperty.Register(nameof(IsBottomPanelVisible), typeof(bool), typeof(WebStatusBarControl),
                new PropertyMetadata(false));

        public bool IsRightSideBarVisible {
            get => (bool)GetValue(IsRightSideBarVisibleProperty);
            set => SetValue(IsRightSideBarVisibleProperty, value);
        }
        public static readonly DependencyProperty IsRightSideBarVisibleProperty =
            DependencyProperty.Register(nameof(IsRightSideBarVisible), typeof(bool), typeof(WebStatusBarControl),
                new PropertyMetadata(true));

        public bool IsDebugRunning {
            get => (bool)GetValue(IsDebugRunningProperty);
            set => SetValue(IsDebugRunningProperty, value);
        }
        public static readonly DependencyProperty IsDebugRunningProperty =
            DependencyProperty.Register(nameof(IsDebugRunning), typeof(bool), typeof(WebStatusBarControl),
                new PropertyMetadata(false, OnIsDebugRunningChanged));

        private static readonly SolidColorBrush _runningBrush = new(Color.FromArgb(255, 80, 140, 200));

        private static void OnIsDebugRunningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var control = (WebStatusBarControl)d;
            if ((bool)e.NewValue) {
                control.rootGrid.Background = _runningBrush;
                VisualStateManager.GoToState(control, "Running", true);
            }
            else {
                control.rootGrid.ClearValue(Grid.BackgroundProperty);
                VisualStateManager.GoToState(control, "Idle", true);
            }
        }

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

        public bool IsSaving {
            get => (bool)GetValue(IsSavingProperty);
            set => SetValue(IsSavingProperty, value);
        }
        public static readonly DependencyProperty IsSavingProperty =
            DependencyProperty.Register(nameof(IsSaving), typeof(bool), typeof(WebStatusBarControl), new PropertyMetadata(false));

        public string IndentText {
            get => (string)GetValue(IndentTextProperty);
            set => SetValue(IndentTextProperty, value);
        }
        public static readonly DependencyProperty IndentTextProperty =
            DependencyProperty.Register(nameof(IndentText), typeof(string), typeof(WebStatusBarControl), new PropertyMetadata(string.Empty));

        public string EncodingText {
            get => (string)GetValue(EncodingTextProperty);
            set => SetValue(EncodingTextProperty, value);
        }
        public static readonly DependencyProperty EncodingTextProperty =
            DependencyProperty.Register(nameof(EncodingText), typeof(string), typeof(WebStatusBarControl), new PropertyMetadata(string.Empty));

        public string LineEndingText {
            get => (string)GetValue(LineEndingTextProperty);
            set => SetValue(LineEndingTextProperty, value);
        }
        public static readonly DependencyProperty LineEndingTextProperty =
            DependencyProperty.Register(nameof(LineEndingText), typeof(string), typeof(WebStatusBarControl), new PropertyMetadata(string.Empty));

        public string CursorPositionText => SelectedCharacterCount > 0
            ? string.Format(
                LanguageUtil.GetI18n("WebBackdrop_CursorPositionSelected"),
                LineNumber,
                Column,
                SelectedCharacterCount,
                IsSelectedCharacterCountOverflow ? "+" : string.Empty)
            : string.Format(LanguageUtil.GetI18n("WebBackdrop_CursorPosition"), LineNumber, Column);
        public string ProblemErrorText => ProblemErrorCount.ToString();
        public string ProblemWarningText => ProblemWarningCount.ToString();
        public Visibility ProblemErrorActiveVisibility => ProblemErrorCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ProblemErrorInactiveVisibility => ProblemErrorCount > 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility ProblemWarningActiveVisibility => ProblemWarningCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ProblemWarningInactiveVisibility => ProblemWarningCount > 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility SavingVisibility => IsSaving ? Visibility.Visible : Visibility.Collapsed;
        public string LanguageStatusText => $"{{ }} {WebEditorFileUtil.FormatLanguage(ActiveFileLanguage)}";

        public bool IsTextFile {
            get => (bool)GetValue(IsTextFileProperty);
            set => SetValue(IsTextFileProperty, value);
        }
        public static readonly DependencyProperty IsTextFileProperty =
            DependencyProperty.Register(nameof(IsTextFile), typeof(bool), typeof(WebStatusBarControl), new PropertyMetadata(false));

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

        private void Problems_Click(object sender, RoutedEventArgs e) {
            PanelRequested?.Invoke(this, WebEditorBottomPanel.Problems);
        }

        private void Run_Click(object sender, RoutedEventArgs e) {
            RunRequested?.Invoke(this, e);
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

        private void Indent_Click(object sender, RoutedEventArgs e) { }

        private void IndentOption_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuFlyoutItem { Tag: string tag }) {
                var parts = tag.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out var tabSize)) {
                    var insertSpaces = parts[1] == "spaces";
                    IndentChanged?.Invoke(this, (tabSize, insertSpaces));
                }
            }
        }

        private void Encoding_Click(object sender, RoutedEventArgs e) { }

        private void EncodingOption_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuFlyoutItem { Tag: string encoding }) {
                EncodingChanged?.Invoke(this, encoding);
            }
        }

        private void LineEnding_Click(object sender, RoutedEventArgs e) { }

        private void LineEndingOption_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuFlyoutItem { Tag: string lineEnding }) {
                LineEndingChanged?.Invoke(this, lineEnding);
            }
        }
    }
}
