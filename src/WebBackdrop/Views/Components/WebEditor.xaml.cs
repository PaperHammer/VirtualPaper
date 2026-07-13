using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.Common.Logging;
using VirtualPaper.Models.Cores;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Windows.System;
using Windows.UI;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.ViewModels;
using Workloads.Creation.WebBackdrop.Views.Tools;

namespace Workloads.Creation.WebBackdrop.Views.Components {
    public sealed partial class WebEditor : ArcUserControl {
        public WebEditorViewModel ViewModel { get; private set; } = null!;

        public WebToolItem? SelectedTool {
            get => (WebToolItem?)GetValue(SelectedToolProperty);
            set => SetValue(SelectedToolProperty, value);
        }
        public static readonly DependencyProperty SelectedToolProperty =
            DependencyProperty.Register(nameof(SelectedTool), typeof(WebToolItem), typeof(WebEditor),
                new PropertyMetadata(null, OnSelectedToolChanged));

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

        private WebProjectSession? _session;
        private WebFileTreeControl? _fileTreeControl;
        private WebProjectInfoControl? _projectInfoControl;
        private string _activeBottomPanel = "PROBLEMS";
        private Dictionary<EditorPanelSlot, PanelLayoutState> _panelLayoutStates = null!;
        private Dictionary<object, Rectangle> _splitterLines = null!;
        private Dictionary<string, Action> _shortcutActions = null!;

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
            KeyboardSinglePressUtil.Instance.AddListener(this);

            if (Payload == null) return;

            Payload.TryGet(NaviPayloadKey.WebProjectSession, out _session);
            if (_session == null) return;

            ViewModel = new WebEditorViewModel(_session);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.OpenFiles.CollectionChanged += OpenFiles_CollectionChanged;

            leftFileTreeControl.ProjectName = _session.DesignFileUtil.ProjectName;
            leftFileTreeControl.Refresh(_session.DesignFileUtil.ProjectFolder);

            var projectData = _session.DesignFileUtil.GetOrCreateProjectData();
            if (projectData != null) {
                _projectInfoControl ??= new WebProjectInfoControl();
                _projectInfoControl.Load(projectData);
                _projectInfoControl.ProjectInfoSaved += ProjectInfo_ProjectInfoSaved;
                toolPanelHost.Children.Add(_projectInfoControl);
            }

            // Open entry file
            var entryFile = _session.DesignFileUtil.EntryFilePath;
            if (File.Exists(entryFile)) {
                ViewModel.OpenFile(entryFile);
            }

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
                ActiveFilePathText = ViewModel.ActiveFile.FilePath;
                ActiveFileLanguage = GetLanguageFromExtension(ViewModel.ActiveFile.FileExtension);
                monacoEditor.EditorContent = ViewModel.ActiveFile.Content;
                monacoEditor.EditorLanguage = ActiveFileLanguage;
            }
            else {
                ActiveFilePathText = string.Empty;
                ActiveFileLanguage = "plaintext";
                monacoEditor.EditorContent = string.Empty;
                monacoEditor.EditorLanguage = ActiveFileLanguage;
            }

            CursorLineNumber = 1;
            CursorColumn = 1;
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
                ViewModel.CloseFile(file);
            }
        }

        private void FileTabView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UpdateStatusBar();
        }

        private void MonacoEditor_ContentChanged(object? sender, string content) {
            if (ViewModel?.ActiveFile != null && ViewModel.ActiveFile.Content != content) {
                ViewModel.ActiveFile.Content = content;
            }
        }

        private void MonacoEditor_CursorPositionChanged(object? sender, MonacoCursorPosition position) {
            CursorLineNumber = position.LineNumber;
            CursorColumn = position.Column;
        }

        private void MonacoEditor_ShortcutRequested(object? sender, string command) {
            if (_shortcutActions.TryGetValue(command, out var action)) {
                action();
            }
        }

        private void ToolList_ToolListLoaded(object? sender, RoutedEventArgs e) {
        }

        private static void OnSelectedToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is WebEditor editor) {
                editor.OnSelectedToolChanged(e.NewValue as WebToolItem);
            }
        }

        private void OnSelectedToolChanged(WebToolItem? tool) {
            if (tool == null) {
                toolPanelHost.Visibility = Visibility.Collapsed;
                return;
            }

            toolPanelHost.Visibility = Visibility.Visible;
            toolPanelHost.Children.Clear();

            switch (tool.Type) {
                case WebToolType.FileTree:
                    _fileTreeControl ??= new WebFileTreeControl();
                    _fileTreeControl.ProjectName = _session?.DesignFileUtil.ProjectName ?? string.Empty;
                    _fileTreeControl.Refresh(_session?.DesignFileUtil.ProjectFolder ?? string.Empty);
                    _fileTreeControl.FileOpenRequested -= FileTree_FileOpenRequested;
                    _fileTreeControl.FileOpenRequested += FileTree_FileOpenRequested;
                    toolPanelHost.Children.Add(_fileTreeControl);
                    break;

                case WebToolType.ProjectInfo:
                    _projectInfoControl ??= new WebProjectInfoControl();
                    var projectData = _session?.DesignFileUtil.GetOrCreateProjectData();
                    if (projectData != null) {
                        _projectInfoControl.Load(projectData);
                    }
                    _projectInfoControl.ProjectInfoSaved += ProjectInfo_ProjectInfoSaved;
                    toolPanelHost.Children.Add(_projectInfoControl);
                    break;
            }
        }

        private void FileTree_FileOpenRequested(object? sender, string filePath) {
            ViewModel?.OpenFile(filePath);
        }

        private void RegisterShortcuts() {
            _shortcutActions = new Dictionary<string, Action> {
                ["toggleLeftSideBar"] = () => TogglePanel(EditorPanelSlot.Left),
                ["toggleBottomPanel"] = ToggleBottomPanel,
                ["toggleRightSideBar"] = () => TogglePanel(EditorPanelSlot.Right),
            };

            KeyboardSinglePressUtil.Instance.RegisterShortcut(
                _shortcutActions["toggleLeftSideBar"],
                VirtualKey.B,
                VirtualKeyModifiers.Control,
                "Toggle left side bar");
            KeyboardSinglePressUtil.Instance.RegisterShortcut(
                _shortcutActions["toggleBottomPanel"],
                VirtualKey.J,
                VirtualKeyModifiers.Control,
                "Toggle bottom panel");
            KeyboardSinglePressUtil.Instance.RegisterShortcut(
                _shortcutActions["toggleRightSideBar"],
                VirtualKey.B,
                VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
                "Toggle right side bar");
        }

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

        private void ProjectInfo_ProjectInfoSaved(object? sender, WpWebProjectData data) {
            _session?.DesignFileUtil.SaveProjectData(data);
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

        private void BottomPanelTab_Click(object sender, RoutedEventArgs e) {
            if (sender is FrameworkElement element && element.Tag is string panel) {
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
            bottomPanelContent.Text = panel switch {
                "PROBLEMS" => "当前没有检测到问题。",
                "OUTPUT" => "暂无输出。",
                "DEBUG CONSOLE" => "调试控制台尚未连接。",
                "TERMINAL" => "终端功能暂未启用。",
                //"PORTS" => "暂无转发端口。",
                _ => string.Empty,
            };
        }

        private void UpdateStatusBarLayoutState() {
            statusBar.IsLeftSideBarVisible = leftSideBar.Visibility == Visibility.Visible;
            statusBar.IsBottomPanelVisible = bottomPanel.Visibility == Visibility.Visible;
            statusBar.IsRightSideBarVisible = rightSideBar.Visibility == Visibility.Visible;
            statusBar.RefreshLayoutState();
        }
    }
}
