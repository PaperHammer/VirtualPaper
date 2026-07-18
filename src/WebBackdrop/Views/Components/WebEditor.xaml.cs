using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.Common.Logging;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Windows.System;
using Windows.UI;
using Windows.UI.Text;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.ViewModels;
using Workloads.Creation.WebBackdrop.Views.Components.BottomPanels;

namespace Workloads.Creation.WebBackdrop.Views.Components {
    public sealed partial class WebEditor : ArcUserControl {
        public WebEditorViewModel ViewModel { get; private set; } = null!;

        public string ActiveFilePathText {
            get => (string)GetValue(ActiveFilePathTextProperty);
            set => SetValue(ActiveFilePathTextProperty, value);
        }
        public static readonly DependencyProperty ActiveFilePathTextProperty =
            DependencyProperty.Register(nameof(ActiveFilePathText), typeof(string), typeof(WebEditor),
                new PropertyMetadata(string.Empty));

        public string ActiveFileLanguage {
            get => (string)GetValue(ActiveFileLanguageProperty);
            set => SetValue(ActiveFileLanguageProperty, value);
        }
        public static readonly DependencyProperty ActiveFileLanguageProperty =
            DependencyProperty.Register(nameof(ActiveFileLanguage), typeof(string), typeof(WebEditor),
                new PropertyMetadata("plaintext"));

        public int CursorLineNumber {
            get => (int)GetValue(CursorLineNumberProperty);
            set => SetValue(CursorLineNumberProperty, value);
        }
        public static readonly DependencyProperty CursorLineNumberProperty =
            DependencyProperty.Register(nameof(CursorLineNumber), typeof(int), typeof(WebEditor),
                new PropertyMetadata(1));

        public int CursorColumn {
            get => (int)GetValue(CursorColumnProperty);
            set => SetValue(CursorColumnProperty, value);
        }
        public static readonly DependencyProperty CursorColumnProperty =
            DependencyProperty.Register(nameof(CursorColumn), typeof(int), typeof(WebEditor),
                new PropertyMetadata(1));

        public int SelectedCharacterCount {
            get => (int)GetValue(SelectedCharacterCountProperty);
            set => SetValue(SelectedCharacterCountProperty, value);
        }
        public static readonly DependencyProperty SelectedCharacterCountProperty =
            DependencyProperty.Register(nameof(SelectedCharacterCount), typeof(int), typeof(WebEditor),
                new PropertyMetadata(0));

        public bool IsSelectedCharacterCountOverflow {
            get => (bool)GetValue(IsSelectedCharacterCountOverflowProperty);
            set => SetValue(IsSelectedCharacterCountOverflowProperty, value);
        }
        public static readonly DependencyProperty IsSelectedCharacterCountOverflowProperty =
            DependencyProperty.Register(nameof(IsSelectedCharacterCountOverflow), typeof(bool), typeof(WebEditor),
                new PropertyMetadata(false));

        public int ProblemErrorCount {
            get => (int)GetValue(ProblemErrorCountProperty);
            set => SetValue(ProblemErrorCountProperty, value);
        }
        public static readonly DependencyProperty ProblemErrorCountProperty =
            DependencyProperty.Register(nameof(ProblemErrorCount), typeof(int), typeof(WebEditor),
                new PropertyMetadata(0));

        public int ProblemWarningCount {
            get => (int)GetValue(ProblemWarningCountProperty);
            set => SetValue(ProblemWarningCountProperty, value);
        }
        public static readonly DependencyProperty ProblemWarningCountProperty =
            DependencyProperty.Register(nameof(ProblemWarningCount), typeof(int), typeof(WebEditor),
                new PropertyMetadata(0));

        private enum EditorPanelSlot {
            Left,
            Bottom,
            Right,
        }

        private sealed class PanelLayoutState {
            public PanelLayoutState(
                FrameworkElement panel,
                FrameworkElement splitter,
                bool resetSizeOnToggle,
                double collapsedSize,
                double expandedSize,
                double minSize,
                double maxSize,
                Func<double> getSize,
                Action<double> setSize,
                Func<ManipulationDeltaRoutedEventArgs, double> getDelta) {
                Panel = panel;
                Splitter = splitter;
                ResetSizeOnToggle = resetSizeOnToggle;
                CollapsedSize = collapsedSize;
                ExpandedSize = expandedSize;
                MinSize = minSize;
                MaxSize = maxSize;
                GetSize = getSize;
                SetSize = setSize;
                GetDelta = getDelta;
            }

            public FrameworkElement Panel { get; }
            public FrameworkElement Splitter { get; }
            public bool ResetSizeOnToggle { get; }
            public double CollapsedSize { get; }
            public double ExpandedSize { get; }
            public double MinSize { get; }
            public double MaxSize { get; }
            public Func<double> GetSize { get; }
            public Action<double> SetSize { get; }
            public Func<ManipulationDeltaRoutedEventArgs, double> GetDelta { get; }
        }

        public WebEditor() {
            InitializeComponent();
            InitializePanelLayoutStates();
            RegisterShortcuts();
        }

        private void ArcUserControl_Loaded(object sender, RoutedEventArgs e) {
            if (Payload == null) return;

            Payload.TryGet(NaviPayloadKey.WebProjectSession, out _session);
            if (_session == null) return;

            ViewModel = new WebEditorViewModel(_session);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.OpenFiles.CollectionChanged += OpenFiles_CollectionChanged;

            leftFileTreeControl.ProjectName = _session.DesignFileUtil.ProjectName;
            leftFileTreeControl.Refresh(_session.DesignFileUtil.ProjectFolder);
            propertyPanelControl.LoadProject(_session.DesignFileUtil);
            problemsPanel.SetProjectFolder(_session.DesignFileUtil.ProjectFolder);

            UpdateStatusBar();
        }

        private void OpenFiles_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            UpdateStatusBar();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(WebEditorViewModel.ActiveFile)) {
                UpdateStatusBar();
            }
        }

        private void UpdateStatusBar() {
            if (ViewModel?.ActiveFile != null) {
                var filePath = ViewModel.ActiveFile.FilePath;
                if (!string.Equals(_activeEditorFilePath, filePath, StringComparison.OrdinalIgnoreCase)) {
                    _activeEditorFilePath = filePath;
                    _ignoredEmptyMarkersFilePath = filePath;
                }

                ActiveFilePathText = filePath;
                ActiveFileLanguage = GetLanguageFromExtension(ViewModel.ActiveFile.FileExtension);
                monacoEditor.EditorContent = ViewModel.ActiveFile.Content;
                monacoEditor.EditorLanguage = ActiveFileLanguage;
                monacoEditor.Visibility = Visibility.Visible;
                welcomePanel.Visibility = Visibility.Collapsed;
            }
            else {
                ActiveFilePathText = string.Empty;
                ActiveFileLanguage = "plaintext";
                monacoEditor.EditorContent = string.Empty;
                monacoEditor.EditorLanguage = ActiveFileLanguage;
                monacoEditor.Visibility = Visibility.Collapsed;
                welcomePanel.Visibility = Visibility.Visible;
            }

            CursorLineNumber = 1;
            CursorColumn = 1;
            SelectedCharacterCount = 0;
            IsSelectedCharacterCountOverflow = false;
            propertyPanelControl.Load(ViewModel?.ActiveFile, ActiveFileLanguage);
            if (ViewModel?.ActiveFile != null) {
                leftFileTreeControl.SelectFile(ViewModel.ActiveFile.FilePath);
            }
            UpdateStatusBarLayoutState();
        }

        private static string GetLanguageFromExtension(string ext) {
            return ext.ToLowerInvariant() switch {
                ".html" or ".htm" => "html",
                ".css" => "css",
                ".js" => "javascript",
                ".json" => "json",
                ".ts" => "typescript",
                ".xml" => "xml",
                ".md" => "markdown",
                _ => "plaintext",
            };
        }

        private void FileTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) {
            if (args.Item is WebEditorFile file) {
                problemsPanel.RemoveFile(file.FilePath);
                RefreshProblemCounts();
                ViewModel.CloseFile(file);
            }
        }

        private void FileTabView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UpdateStatusBar();
        }

        #region MonacoEditor event handlers
        private void MonacoEditor_ContentChanged(object? sender, string content) {
            if (ViewModel?.ActiveFile != null && ViewModel.ActiveFile.Content != content) {
                ViewModel.ActiveFile.Content = content;
                propertyPanelControl.Load(ViewModel.ActiveFile, ActiveFileLanguage);
            }
        }

        private void MonacoEditor_CursorPositionChanged(object? sender, MonacoCursorPosition position) {
            CursorLineNumber = position.LineNumber;
            CursorColumn = position.Column;
            SelectedCharacterCount = position.SelectedCharacterCount;
            IsSelectedCharacterCountOverflow = position.IsSelectedCharacterCountOverflow;
        }

        private void MonacoEditor_MarkersChanged(object? sender, IReadOnlyList<MonacoMarker> markers) {
            if (ViewModel?.ActiveFile != null
                && markers.Count == 0
                && string.Equals(_ignoredEmptyMarkersFilePath, ViewModel.ActiveFile.FilePath, StringComparison.OrdinalIgnoreCase)) {
                _ignoredEmptyMarkersFilePath = null;
                return;
            }

            _ignoredEmptyMarkersFilePath = null;
            UpdateProblems(markers);
        }

        private void MonacoEditor_ShortcutRequested(object? sender, string command) {
            InvokeShortcut(command);
        }
        #endregion

        private void FileTree_FileOpenRequested(object? sender, string filePath) {
            ViewModel?.OpenFile(filePath);
        }

        private void FileTree_FolderSelected(object? sender, string folderPath) {
            propertyPanelControl.LoadFolder(folderPath);
        }

        #region keyboard accelerators
        private void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
            var command = (sender.Key, sender.Modifiers) switch {
                (VirtualKey.B, VirtualKeyModifiers.Control) => "toggleLeftSideBar",
                (VirtualKey.J, VirtualKeyModifiers.Control) => "toggleBottomPanel",
                (VirtualKey.B, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu) => "toggleRightSideBar",
                _ => null,
            };

            if (command == null) return;

            InvokeShortcut(command);
            args.Handled = true;
        }

        private void InvokeShortcut(string command) {
            if (_shortcutActions.TryGetValue(command, out var action)) {
                action();
            }
        }

        private void RegisterShortcuts() {
            _shortcutActions = new Dictionary<string, Action> {
                ["toggleLeftSideBar"] = () => TogglePanel(EditorPanelSlot.Left),
                ["toggleBottomPanel"] = ToggleBottomPanel,
                ["toggleRightSideBar"] = () => TogglePanel(EditorPanelSlot.Right),
            };
        }
        #endregion

        private void ToggleBottomPanel() {
            Action updateBottomPanel = bottomPanel.Visibility == Visibility.Visible
                ? HideBottomPanel
                : () => ShowBottomPanel(string.IsNullOrEmpty(_activeBottomPanel) ? "PROBLEMS" : _activeBottomPanel);
            updateBottomPanel();
        }

        private void InitializePanelLayoutStates() {
            _panelLayoutStates = new Dictionary<EditorPanelSlot, PanelLayoutState> {
                [EditorPanelSlot.Left] = new(
                    leftSideBar,
                    leftSideBarSplitter,
                    true,
                    0,
                    260,
                    180,
                    480,
                    () => leftSideBarColumn.Width.Value,
                    value => leftSideBarColumn.Width = new GridLength(value),
                    e => e.Delta.Translation.X),
                [EditorPanelSlot.Bottom] = new(
                    bottomPanel,
                    bottomSideBarSplitter,
                    false,
                    0,
                    240,
                    bottomPanel.MinHeight,
                    480,
                    () => bottomPanel.Height,
                    value => bottomPanel.Height = value,
                    e => -e.Delta.Translation.Y),
                [EditorPanelSlot.Right] = new(
                    rightSideBar,
                    rightSideBarSplitter,
                    true,
                    0,
                    280,
                    200,
                    600,
                    () => rightSideBarColumn.Width.Value,
                    value => rightSideBarColumn.Width = new GridLength(value),
                    e => -e.Delta.Translation.X),
            };

            _splitterLines = new Dictionary<object, Rectangle> {
                [leftSideBarSplitter] = leftSideBarSplitterLine,
                [bottomSideBarSplitter] = bottomSideBarSplitterLine,
                [rightSideBarSplitter] = rightSideBarSplitterLine,
            };
        }

        private void ResizePanel(EditorPanelSlot slot, ManipulationDeltaRoutedEventArgs e) {
            var state = _panelLayoutStates[slot];
            if (state.Panel.Visibility != Visibility.Visible) return;

            var size = state.GetSize() + state.GetDelta(e);
            state.SetSize(Math.Clamp(size, state.MinSize, state.MaxSize));
        }

        private void SetPanelVisibility(EditorPanelSlot slot, bool isVisible) {
            var state = _panelLayoutStates[slot];
            var visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            state.Panel.Visibility = visibility;
            state.Splitter.Visibility = visibility;

            if (state.ResetSizeOnToggle) {
                state.SetSize(isVisible ? state.ExpandedSize : state.CollapsedSize);
            }

            UpdateStatusBarLayoutState();
        }

        private void TogglePanel(EditorPanelSlot slot) {
            SetPanelVisibility(slot, _panelLayoutStates[slot].Panel.Visibility != Visibility.Visible);
        }

        private void LeftSideBarSplitter_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e) {
            ResizePanel(EditorPanelSlot.Left, e);
        }

        private void RightSideBarSplitter_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e) {
            ResizePanel(EditorPanelSlot.Right, e);
        }

        private void BottomSideBarSplitter_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e) {
            ResizePanel(EditorPanelSlot.Bottom, e);
        }

        private void Splitter_PointerEntered(object sender, PointerRoutedEventArgs e) {
            if (_splitterLines.TryGetValue(sender, out var line)) {
                line.Fill = new SolidColorBrush(Color.FromArgb(255, 0, 122, 204));
            }
        }

        private void Splitter_PointerExited(object sender, PointerRoutedEventArgs e) {
            if (_splitterLines.TryGetValue(sender, out var line)) {
                line.ClearValue(Shape.FillProperty);
            }
        }

        private void StatusBar_PanelRequested(object? sender, string panel) {
            var isCurrentPanelVisible = bottomPanel.Visibility == Visibility.Visible && _activeBottomPanel == panel;
            Action updateBottomPanel = isCurrentPanelVisible ? HideBottomPanel : () => ShowBottomPanel(panel);
            updateBottomPanel();
        }

        private void StatusBar_RunRequested(object? sender, RoutedEventArgs e) {
            ArcLog.GetLogger<WebEditor>().Info("Run requested");
        }

        private void StatusBar_DebugRequested(object? sender, RoutedEventArgs e) {
            ArcLog.GetLogger<WebEditor>().Info("Debug requested");
        }

        private void StatusBar_ToggleLeftSideBarRequested(object? sender, RoutedEventArgs e) {
            TogglePanel(EditorPanelSlot.Left);
        }

        private void StatusBar_ToggleBottomPanelRequested(object? sender, RoutedEventArgs e) {
            ToggleBottomPanel();
        }

        private void StatusBar_ToggleRightSideBarRequested(object? sender, RoutedEventArgs e) {
            TogglePanel(EditorPanelSlot.Right);
        }

        private void BottomPanelSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e) {
            if (sender.SelectedItem is SelectorBarItem { Tag: string panel }) {
                ShowBottomPanel(panel);
            }
        }

        private void BottomPanelClose_Click(object sender, RoutedEventArgs e) {
            HideBottomPanel();
        }

        private void ShowBottomPanel(string panel) {
            SetBottomPanel(panel);
            SetPanelVisibility(EditorPanelSlot.Bottom, true);
        }

        private void HideBottomPanel() {
            SetPanelVisibility(EditorPanelSlot.Bottom, false);
        }

        private void SetBottomPanel(string panel) {
            _activeBottomPanel = panel;
            UpdateBottomPanelTabStates();
            problemsPanel.Visibility = panel == "PROBLEMS" && problemsPanel.ProblemCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            bottomPanelContent.Visibility = panel == "PROBLEMS" && problemsPanel.ProblemCount > 0 ? Visibility.Collapsed : Visibility.Visible;
            bottomPanelContent.Text = panel switch {
                "PROBLEMS" => "当前没有检测到问题。",
                "OUTPUT" => "暂无输出。",
                _ => string.Empty,
            };
        }

        private void UpdateBottomPanelTabStates() {
            foreach (var item in bottomPanelSelectorBar.Items) {
                if (item is SelectorBarItem selectorBarItem && selectorBarItem.Tag is string panel) {
                    selectorBarItem.IsSelected = panel == _activeBottomPanel;
                }
            }
        }

        private void UpdateProblems(IReadOnlyList<MonacoMarker> markers) {
            if (ViewModel?.ActiveFile == null) return;

            problemsPanel.UpdateProblems(ViewModel.ActiveFile.FilePath, markers);
            RefreshProblemCounts();
        }

        private void RefreshProblemCounts() {
            ProblemErrorCount = problemsPanel.ErrorCount;
            ProblemWarningCount = problemsPanel.WarningCount;

            // 问题数量变化时，刷新 Problems 面板显示状态。避免有问题了，但底部仍显示“当前没有检测到问题”
            if (_activeBottomPanel == "PROBLEMS") {
                SetBottomPanel(_activeBottomPanel);
            }
        }

        private async void ProblemsPanel_ProblemRequested(object? sender, ProblemItem item) {
            ViewModel.OpenFile(item.FilePath);
            await Task.Delay(50);
            await monacoEditor.RevealPositionAsync(item.LineNumber, item.ColumnNumber);
        }

        private void UpdateStatusBarLayoutState() {
            statusBar.IsLeftSideBarVisible = leftSideBar.Visibility == Visibility.Visible;
            statusBar.IsBottomPanelVisible = bottomPanel.Visibility == Visibility.Visible;
            statusBar.IsRightSideBarVisible = rightSideBar.Visibility == Visibility.Visible;
            statusBar.RefreshLayoutState();
        }

        private WebProjectSession? _session;
        private string _activeBottomPanel = "PROBLEMS";
        private string? _activeEditorFilePath;
        private string? _ignoredEmptyMarkersFilePath;
        private Dictionary<EditorPanelSlot, PanelLayoutState> _panelLayoutStates = null!;
        private Dictionary<object, Rectangle> _splitterLines = null!;
        private Dictionary<string, Action> _shortcutActions = null!;
    }
}