using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.ProjectSystem.Events;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Windows.System;
using Workloads.Creation.WebBackdrop.Core.Theme;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.ViewModels;
using Workloads.Creation.WebBackdrop.Views.Components.BottomPanels;

namespace Workloads.Creation.WebBackdrop.Views.Components {
    public sealed partial class WebEditor : ArcUserControl {
        public event EventHandler? SaveRequested;
        public event EventHandler? SaveAllRequested;

        public WebEditorViewModel ViewModel { get; private set; } = null!;

        public string ActiveFileLanguage {
            get => (string)GetValue(ActiveFileLanguageProperty);
            set => SetValue(ActiveFileLanguageProperty, value);
        }
        public static readonly DependencyProperty ActiveFileLanguageProperty =
            DependencyProperty.Register(nameof(ActiveFileLanguage), typeof(string), typeof(WebEditor), new PropertyMetadata("plaintext"));

        public int CursorLineNumber {
            get => (int)GetValue(CursorLineNumberProperty);
            set => SetValue(CursorLineNumberProperty, value);
        }
        public static readonly DependencyProperty CursorLineNumberProperty =
            DependencyProperty.Register(nameof(CursorLineNumber), typeof(int), typeof(WebEditor), new PropertyMetadata(1));

        public int CursorColumn {
            get => (int)GetValue(CursorColumnProperty);
            set => SetValue(CursorColumnProperty, value);
        }
        public static readonly DependencyProperty CursorColumnProperty =
            DependencyProperty.Register(nameof(CursorColumn), typeof(int), typeof(WebEditor), new PropertyMetadata(1));

        public int SelectedCharacterCount {
            get => (int)GetValue(SelectedCharacterCountProperty);
            set => SetValue(SelectedCharacterCountProperty, value);
        }
        public static readonly DependencyProperty SelectedCharacterCountProperty =
            DependencyProperty.Register(nameof(SelectedCharacterCount), typeof(int), typeof(WebEditor), new PropertyMetadata(0));

        public bool IsSelectedCharacterCountOverflow {
            get => (bool)GetValue(IsSelectedCharacterCountOverflowProperty);
            set => SetValue(IsSelectedCharacterCountOverflowProperty, value);
        }
        public static readonly DependencyProperty IsSelectedCharacterCountOverflowProperty =
            DependencyProperty.Register(nameof(IsSelectedCharacterCountOverflow), typeof(bool), typeof(WebEditor), new PropertyMetadata(false));

        public int ProblemErrorCount {
            get => (int)GetValue(ProblemErrorCountProperty);
            set => SetValue(ProblemErrorCountProperty, value);
        }
        public static readonly DependencyProperty ProblemErrorCountProperty =
            DependencyProperty.Register(nameof(ProblemErrorCount), typeof(int), typeof(WebEditor), new PropertyMetadata(0));

        public int ProblemWarningCount {
            get => (int)GetValue(ProblemWarningCountProperty);
            set => SetValue(ProblemWarningCountProperty, value);
        }
        public static readonly DependencyProperty ProblemWarningCountProperty =
            DependencyProperty.Register(nameof(ProblemWarningCount), typeof(int), typeof(WebEditor), new PropertyMetadata(0));

        public bool IsSaving {
            get => (bool)GetValue(IsSavingProperty);
            set => SetValue(IsSavingProperty, value);
        }
        public static readonly DependencyProperty IsSavingProperty =
            DependencyProperty.Register(nameof(IsSaving), typeof(bool), typeof(WebEditor), new PropertyMetadata(false));

        public string IndentText {
            get => (string)GetValue(IndentTextProperty);
            set => SetValue(IndentTextProperty, value);
        }
        public static readonly DependencyProperty IndentTextProperty =
            DependencyProperty.Register(nameof(IndentText), typeof(string), typeof(WebEditor), new PropertyMetadata(string.Empty));

        public string EncodingText {
            get => (string)GetValue(EncodingTextProperty);
            set => SetValue(EncodingTextProperty, value);
        }
        public static readonly DependencyProperty EncodingTextProperty =
            DependencyProperty.Register(nameof(EncodingText), typeof(string), typeof(WebEditor), new PropertyMetadata(string.Empty));

        public string LineEndingText {
            get => (string)GetValue(LineEndingTextProperty);
            set => SetValue(LineEndingTextProperty, value);
        }
        public static readonly DependencyProperty LineEndingTextProperty =
            DependencyProperty.Register(nameof(LineEndingText), typeof(string), typeof(WebEditor), new PropertyMetadata(string.Empty));

        public bool ActiveFileIsText {
            get => (bool)GetValue(ActiveFileIsTextProperty);
            set => SetValue(ActiveFileIsTextProperty, value);
        }
        public static readonly DependencyProperty ActiveFileIsTextProperty =
            DependencyProperty.Register(nameof(ActiveFileIsText), typeof(bool), typeof(WebEditor),
                new PropertyMetadata(false));

        private enum EditorPanelSlot {
            Left,
            Bottom,
            Right,
        }

        private static class PanelLayoutDefaults {
            public const double CollapsedSize = 0;
            public const double LeftExpandedWidth = 260;
            public const double LeftMinWidth = 180;
            public const double LeftMaxWidth = 480;
            public const double BottomExpandedHeight = 240;
            public const double BottomMaxHeight = 480;
            public const double RightExpandedWidth = 280;
            public const double RightMinWidth = 200;
            public const double RightMaxWidth = 600;
        }

        private enum WebEditorCommand {
            ToggleLeftSideBar,
            ToggleBottomPanel,
            ToggleRightSideBar,
            Save,
            SaveAll,
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
                Func<double, double> getSizeDelta) {
                Panel = panel;
                Splitter = splitter;
                ResetSizeOnToggle = resetSizeOnToggle;
                CollapsedSize = collapsedSize;
                ExpandedSize = expandedSize;
                MinSize = minSize;
                MaxSize = maxSize;
                GetSize = getSize;
                SetSize = setSize;
                GetSizeDelta = getSizeDelta;
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
            public Func<double, double> GetSizeDelta { get; }
        }

        public WebEditor() {
            InitializeComponent();
            InitializePanelLayoutStates();
            RegisterCommands();
        }

        private void ArcUserControl_Loaded(object sender, RoutedEventArgs e) {
            if (_isLoaded || Payload == null) return;

            Payload.TryGet(NaviPayloadKey.WebProjectSession, out _session);
            Payload.TryGet(NaviPayloadKey.ContextKey, out ArcPageContextKey contextKey);
            if (_session == null) return;

            _isLoaded = true;
            ViewModel = new WebEditorViewModel(_session, contextKey);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.DebugSessionEnded += OnDebugSessionEnded;

            _session.FileManager.Changed += FileManager_Changed;

            leftFileTreeControl.Refresh(_session.DesignFileUtil);
            leftFileTreeControl.ProjectFileRenamed += OnProjectFileRenamed;
            propertyPanelControl.LoadProject(_session.DesignFileUtil);
            problemsPanel.SetProjectFolder(_session.DesignFileUtil.ProjectFolder);

            editorContentView.TextEditor.FileOpenRequested += OnMonacoFileOpenRequested;
            editorContentView.TextEditor.NavigationRequested += OnMonacoNavigationRequested;
            editorContentView.MarkdownEditor.MonacoEditor.FileOpenRequested += OnMonacoFileOpenRequested;
            editorContentView.MarkdownEditor.MonacoEditor.NavigationRequested += OnMonacoNavigationRequested;

            UpdateStatusBar();
        }

        private void ArcUserControl_Unloaded(object sender, RoutedEventArgs e) {
            _propertyPanelRefreshCancellation?.Cancel();
            _propertyPanelRefreshCancellation?.Dispose();
            _propertyPanelRefreshCancellation = null;
            if (_session != null) {
                _session.FileManager.Changed -= FileManager_Changed;
            }

            if (ViewModel != null) {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.DebugSessionEnded -= OnDebugSessionEnded;
            }

            editorContentView.TextEditor.FileOpenRequested -= OnMonacoFileOpenRequested;
            editorContentView.TextEditor.NavigationRequested -= OnMonacoNavigationRequested;
            editorContentView.MarkdownEditor.MonacoEditor.FileOpenRequested -= OnMonacoFileOpenRequested;
            editorContentView.MarkdownEditor.MonacoEditor.NavigationRequested -= OnMonacoNavigationRequested;

            editorContentView.ReleaseResources();

            // Clean up run/debug temp files
            ViewModel?.CleanupSessions();
            _isDebugRunning = false;
            statusBar.IsDebugRunning = false;

            _isLoaded = false;
        }

        private void OnProjectFileRenamed(object? sender, string newPath) {
            _ = ViewModel?.UpdateRecentUsedAsync(newPath);
            SaveAllRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task OpenFileAsync(string filePath) {
            if (ViewModel == null) return;

            await SyncActiveEditorContentAsync();
            await ViewModel.OpenFileAsync(filePath);
        }

        private async void OnMonacoFileOpenRequested(object? sender, string filePath) {
            if (ViewModel != null && File.Exists(filePath)) {
                await OpenFileAsync(filePath);
                leftFileTreeControl.SelectFile(filePath);
            }
        }

        private async void OnMonacoNavigationRequested(object? sender, string filePath) {
            // Handle Go Back / Go Forward cross-file navigation.
            // JS has already stored the target line/column in _pendingNavigation;
            // we just need to open the file, and setValue() will apply the position.
            if (ViewModel != null && File.Exists(filePath)) {
                await OpenFileAsync(filePath);
                leftFileTreeControl.SelectFile(filePath);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(WebEditorViewModel.ActiveFile)) {
                CheckAndRefreshActiveFile();
                UpdateStatusBar();
            }
        }

        private async void CheckAndRefreshActiveFile() {
            if (ViewModel?.ActiveFile == null || _session == null) return;

            var filePath = ViewModel.ActiveFile.FilePath;
            var fileManager = _session.FileManager;

            if (fileManager.TryConsumeExternalChange(filePath, out var changeType) && changeType == FileChangeType.Changed) {
                try {
                    await ViewModel.ActiveFile.ReopenWithEncodingAsync(ViewModel.ActiveFile.EncodingText);
                }
                catch (Exception ex) {
                    ArcLog.GetLogger<WebEditor>().Error($"Failed to reload externally changed file: {filePath}", ex);
                }
            }
        }

        private void FileManager_Changed(ProjectChangedEvent e) {
            DispatcherQueue.TryEnqueue(() => {
                // 所有文件系统变更统一交给文件树处理（增量更新）
                leftFileTreeControl.ApplyChange(e);

                switch (e.Type) {
                    case ProjectChangeType.Modified:
                    case ProjectChangeType.Reloaded:
                        HandleFileReloaded(e.Path);
                        break;

                    case ProjectChangeType.Created:
                    case ProjectChangeType.Deleted:
                    case ProjectChangeType.Renamed:
                        break;

                    case ProjectChangeType.Conflict:
                        HandleFileConflict(e.Path);
                        break;
                }
            });
        }

        private async void HandleFileReloaded(string filePath) {
            if (ViewModel == null) return;

            var openFile = ViewModel.GetOpenFile(filePath);
            if (openFile == null) return;

            try {
                await openFile.ReopenWithEncodingAsync(openFile.EncodingText);

                if (ViewModel.ActiveFile?.Equals(openFile) == true) {
                    editorContentView.ReloadContent(openFile, ActiveFileLanguage);
                }
            }
            catch (Exception ex) {
                openFile.SetLoadFailed();
                ArcLog.GetLogger<WebEditor>().Error($"Failed to reload file: {filePath}", ex);
                GlobalMessageUtil.ShowError($"Failed to reload file: {filePath}\nThe file may be corrupted or unreadable.\n{ex.Message}");
            }
        }

        private void HandleFileConflict(string filePath) {
            if (ViewModel == null) return;

            var openFile = ViewModel.GetOpenFile(filePath);
            if (openFile == null) return;

            ArcLog.GetLogger<WebEditor>().Warn(
                $"Conflict detected: {filePath} was modified externally while unsaved changes exist. " +
                "User should be prompted to reload or keep.");
        }

        private void UpdateStatusBar() {
            var activeFile = ViewModel?.ActiveFile;
            if (activeFile != null) {
                var filePath = activeFile.FilePath;
                if (!string.Equals(_activeEditorFilePath, filePath, StringComparison.OrdinalIgnoreCase)) {
                    _activeEditorFilePath = filePath;
                }

                ActiveFileLanguage = WebEditorFileUtil.GetLanguageFromExtension(activeFile.FileExtension);
                EncodingText = activeFile.EncodingText;
                ActiveFileIsText = activeFile.CanOpenAsText;
                editorContentView.LoadFile(activeFile, ActiveFileLanguage);
                leftFileTreeControl.SelectFile(filePath);

                SyncEncodingToMonaco(activeFile.EncodingText);
            }
            else {
                ActiveFileLanguage = WebEditorFileUtil.DefaultLanguage;
                IndentText = string.Empty;
                EncodingText = string.Empty;
                LineEndingText = string.Empty;
                ActiveFileIsText = false;
                editorContentView.LoadFile(null, ActiveFileLanguage);
            }

            CursorLineNumber = 1;
            CursorColumn = 1;
            SelectedCharacterCount = 0;
            IsSelectedCharacterCountOverflow = false;
            propertyPanelControl.Load(activeFile, ActiveFileLanguage);
            UpdateStatusBarLayoutState();
        }

        #region editor content event handlers
        private void EditorContentView_ContentModified(object? sender, EventArgs e) {
            if (ViewModel?.ActiveFile is { } activeFile) {
                activeFile.SetSavedState(false);
                SyncFileSavedState(activeFile);
            }
        }

        private void EditorContentView_ContentChanged(object? sender, string content) {
            if (ViewModel?.ActiveFile != null && ViewModel.ActiveFile.Content != content) {
                ViewModel.ActiveFile.Content = content;
                LineEndingText = ViewModel.ActiveFile.LineEndingText;
                QueuePropertyPanelRefresh(ViewModel.ActiveFile, ActiveFileLanguage);
            }
        }

        private void EditorContentView_EditorStateChanged(object? sender, MonacoEditorState state) {
            if (ViewModel?.ActiveFile == null) return;

            var wasSaved = ViewModel.ActiveFile.IsSaved;
            ViewModel.ActiveFile.SetSavedState(state.IsSaved);
            ViewModel.ActiveFile.SetLineEnding(state.LineEnding);
            ViewModel.ActiveFile.SetEncoding(state.Encoding);
            IndentText = state.Indent;
            EncodingText = state.Encoding;
            LineEndingText = state.LineEnding;
            SyncFileSavedState(ViewModel.ActiveFile);

            // Refresh the property panel when IsSaved toggles
            // (e.g. after undo back to last-saved state).
            if (wasSaved != ViewModel.ActiveFile.IsSaved) {
                propertyPanelControl.Load(ViewModel.ActiveFile, ActiveFileLanguage);
            }
        }

        private void SyncFileSavedState(WebEditorFile file) {
            leftFileTreeControl.SetFileSaved(file.FilePath, file.IsSaved);
            _session?.RaiseIsSavedChanged(ViewModel.IsAllSaved);
        }

        public Task UndoAsync() {
            return editorContentView.UndoAsync();
        }

        public Task RedoAsync() {
            return editorContentView.RedoAsync();
        }

        public async Task<bool> SaveAllAsync() {
            await _saveLock.WaitAsync();
            try {
                IsSaving = true;
                if (ViewModel == null) return false;

                await SyncActiveEditorContentAsync();
                var activeFile = ViewModel.ActiveFile;
                var result = await ViewModel.SaveAllAsync();
                if (result && activeFile != null) {
                    await editorContentView.MarkSavedAsync();
                    foreach (var file in ViewModel.OpenFiles) {
                        leftFileTreeControl.SetFileSaved(file.FilePath, file.IsSaved);
                    }
                    // 若保存了项目文件 (.vpw)，增量同步文件树
                    if (ViewModel.OpenFiles.Any(f => _session?.DesignFileUtil.IsProjectFile(f.FilePath) == true)) {
                        leftFileTreeControl.SyncManifest(_session!.DesignFileUtil);
                    }
                    _session?.RaiseIsSavedChanged(ViewModel.IsAllSaved);
                }
                return result;
            }
            finally {
                IsSaving = false;
                _saveLock.Release();
            }
        }

        public async Task<bool> SaveActiveFileAsync() {
            await _saveLock.WaitAsync();
            try {
                IsSaving = true;
                if (ViewModel == null) return false;

                await SyncActiveEditorContentAsync();
                var activeFile = ViewModel.ActiveFile;
                var result = await ViewModel.SaveActiveFileAsync();
                if (result && activeFile != null) {
                    await editorContentView.MarkSavedAsync();
                    leftFileTreeControl.SetFileSaved(activeFile.FilePath, activeFile.IsSaved);
                    // Refresh property panel to reflect updated Save state
                    propertyPanelControl.Load(activeFile, ActiveFileLanguage);
                    // 若保存了项目文件 (.vpw)，增量同步文件树
                    if (_session?.DesignFileUtil.IsProjectFile(activeFile.FilePath) == true) {
                        leftFileTreeControl.SyncManifest(_session.DesignFileUtil);
                    }
                    _session?.RaiseIsSavedChanged(ViewModel.IsAllSaved);
                }
                return result;
            }
            finally {
                IsSaving = false;
                _saveLock.Release();
            }
        }

        private async Task SyncActiveEditorContentAsync() {
            var activeFile = ViewModel?.ActiveFile;
            if (activeFile?.CanOpenAsText != true) return;

            var content = await editorContentView.GetContentAsync();
            if (activeFile.Content != content) {
                activeFile.Content = content;
                QueuePropertyPanelRefresh(activeFile, ActiveFileLanguage);
            }
        }

        private void EditorContentView_CursorPositionChanged(object? sender, MonacoCursorPosition position) {
            CursorLineNumber = position.LineNumber;
            CursorColumn = position.Column;
            SelectedCharacterCount = position.SelectedCharacterCount;
            IsSelectedCharacterCountOverflow = position.IsSelectedCharacterCountOverflow;
        }

        private void EditorContentView_MarkersChanged(object? sender, IReadOnlyList<MonacoMarker> markers) {
            UpdateProblems(markers);
        }

        private void EditorContentView_ShortcutRequested(object? sender, string command) {
            InvokeShortcut(command);
        }
        #endregion

        private async void FileTree_FileOpenRequested(object? sender, string filePath) {
            await OpenFileAsync(filePath);
        }

        private void FileTree_FolderSelected(object? sender, string folderPath) {
            propertyPanelControl.LoadFolder(folderPath);
        }

        #region commands
        private void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
            if (_keyboardCommands.TryGetValue((sender.Key, sender.Modifiers), out var command)) {
                ExecuteCommand(command);
                args.Handled = true;
            }
        }

        private void InvokeShortcut(string command) {
            if (_monacoCommands.TryGetValue(command, out var editorCommand)) {
                ExecuteCommand(editorCommand);
            }
        }

        private void ExecuteCommand(WebEditorCommand command) {
            if (_commandActions.TryGetValue(command, out var action)) {
                action();
            }
        }

        private void RegisterCommands() {
            _commandActions = new Dictionary<WebEditorCommand, Action> {
                [WebEditorCommand.ToggleLeftSideBar] = () => TogglePanel(EditorPanelSlot.Left),
                [WebEditorCommand.ToggleBottomPanel] = ToggleBottomPanel,
                [WebEditorCommand.ToggleRightSideBar] = () => TogglePanel(EditorPanelSlot.Right),
                [WebEditorCommand.Save] = () => SaveRequested?.Invoke(this, EventArgs.Empty),
                [WebEditorCommand.SaveAll] = () => SaveAllRequested?.Invoke(this, EventArgs.Empty),
            };

            _keyboardCommands = new Dictionary<(VirtualKey Key, VirtualKeyModifiers Modifiers), WebEditorCommand>(KeyboardCommandMap);

            _monacoCommands = new Dictionary<string, WebEditorCommand>(MonacoCommandMap);
        }
        #endregion

        private static readonly IReadOnlyDictionary<(VirtualKey Key, VirtualKeyModifiers Modifiers), WebEditorCommand> KeyboardCommandMap =
            new Dictionary<(VirtualKey Key, VirtualKeyModifiers Modifiers), WebEditorCommand> {
                [(VirtualKey.B, VirtualKeyModifiers.Control)] = WebEditorCommand.ToggleLeftSideBar,
                [(VirtualKey.J, VirtualKeyModifiers.Control)] = WebEditorCommand.ToggleBottomPanel,
                [(VirtualKey.B, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu)] = WebEditorCommand.ToggleRightSideBar,
                [(VirtualKey.S, VirtualKeyModifiers.Control)] = WebEditorCommand.Save,
                [(VirtualKey.S, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)] = WebEditorCommand.SaveAll,
            };

        private static readonly IReadOnlyDictionary<string, WebEditorCommand> MonacoCommandMap =
            new Dictionary<string, WebEditorCommand> {
                ["toggleLeftSideBar"] = WebEditorCommand.ToggleLeftSideBar,
                ["toggleBottomPanel"] = WebEditorCommand.ToggleBottomPanel,
                ["toggleRightSideBar"] = WebEditorCommand.ToggleRightSideBar,
                ["save"] = WebEditorCommand.Save,
                ["saveAll"] = WebEditorCommand.SaveAll,
            };

        private void ToggleBottomPanel() {
            Action updateBottomPanel = bottomPanel.Visibility == Visibility.Visible
                ? HideBottomPanel
                : () => ShowBottomPanel(_activeBottomPanel);
            updateBottomPanel();
        }

        private void InitializePanelLayoutStates() {
            _panelLayoutStates = new Dictionary<EditorPanelSlot, PanelLayoutState> {
                [EditorPanelSlot.Left] = CreateColumnPanelState(
                    leftSideBar,
                    leftSideBarSplitter,
                    leftSideBarColumn,
                    PanelLayoutDefaults.LeftExpandedWidth,
                    PanelLayoutDefaults.LeftMinWidth,
                    PanelLayoutDefaults.LeftMaxWidth,
                    delta => delta),
                [EditorPanelSlot.Bottom] = CreateHeightPanelState(
                    bottomPanel,
                    bottomSideBarSplitter,
                    PanelLayoutDefaults.BottomExpandedHeight,
                    bottomPanel.MinHeight,
                    PanelLayoutDefaults.BottomMaxHeight,
                    delta => -delta),
                [EditorPanelSlot.Right] = CreateColumnPanelState(
                    rightSideBar,
                    rightSideBarSplitter,
                    rightSideBarColumn,
                    PanelLayoutDefaults.RightExpandedWidth,
                    PanelLayoutDefaults.RightMinWidth,
                    PanelLayoutDefaults.RightMaxWidth,
                    delta => -delta),
            };

            _splitterLines = new Dictionary<object, Microsoft.UI.Xaml.Shapes.Rectangle> {
                [leftSideBarSplitter] = leftSideBarSplitterLine,
                [bottomSideBarSplitter] = bottomSideBarSplitterLine,
                [rightSideBarSplitter] = rightSideBarSplitterLine,
            };
        }

        private static PanelLayoutState CreateColumnPanelState(
            FrameworkElement panel,
            FrameworkElement splitter,
            ColumnDefinition column,
            double expandedWidth,
            double minWidth,
            double maxWidth,
            Func<double, double> getSizeDelta) {
            return new PanelLayoutState(
                panel,
                splitter,
                true,
                PanelLayoutDefaults.CollapsedSize,
                expandedWidth,
                minWidth,
                maxWidth,
                () => column.Width.Value,
                value => column.Width = new GridLength(value),
                getSizeDelta);
        }

        private static PanelLayoutState CreateHeightPanelState(
            FrameworkElement panel,
            FrameworkElement splitter,
            double expandedHeight,
            double minHeight,
            double maxHeight,
            Func<double, double> getSizeDelta) {
            return new PanelLayoutState(
                panel,
                splitter,
                false,
                PanelLayoutDefaults.CollapsedSize,
                expandedHeight,
                minHeight,
                maxHeight,
                () => panel.Height,
                value => panel.Height = value,
                getSizeDelta);
        }

        private void ResizePanel(EditorPanelSlot slot, double pointerPosition) {
            var state = _panelLayoutStates[slot];
            if (state.Panel.Visibility != Visibility.Visible || _activeResizeSlot != slot) return;

            var pointerDelta = pointerPosition - _resizeStartPointerPosition;
            var size = _resizeStartSize + state.GetSizeDelta(pointerDelta);
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

        private void StartResize(EditorPanelSlot slot, PointerRoutedEventArgs e) {
            var state = _panelLayoutStates[slot];
            if (state.Panel.Visibility != Visibility.Visible || e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse) return;

            _activeResizeSlot = slot;
            _resizePointer = e.Pointer;
            _resizeStartSize = state.GetSize();
            _resizeStartPointerPosition = GetPointerPosition(slot, e);
            state.Splitter.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private double GetPointerPosition(EditorPanelSlot slot, PointerRoutedEventArgs e) {
            var position = e.GetCurrentPoint(editorGrid).Position;
            return slot == EditorPanelSlot.Bottom ? position.Y : position.X;
        }

        private void LeftSideBarSplitter_PointerPressed(object sender, PointerRoutedEventArgs e) {
            StartResize(EditorPanelSlot.Left, e);
        }

        private void RightSideBarSplitter_PointerPressed(object sender, PointerRoutedEventArgs e) {
            StartResize(EditorPanelSlot.Right, e);
        }

        private void BottomSideBarSplitter_PointerPressed(object sender, PointerRoutedEventArgs e) {
            StartResize(EditorPanelSlot.Bottom, e);
        }

        private void Splitter_PointerMoved(object sender, PointerRoutedEventArgs e) {
            if (_activeResizeSlot == null || sender is not FrameworkElement splitter || splitter != _panelLayoutStates[_activeResizeSlot.Value].Splitter) return;

            ResizePanel(_activeResizeSlot.Value, GetPointerPosition(_activeResizeSlot.Value, e));
            e.Handled = true;
        }

        private void Splitter_PointerReleased(object sender, PointerRoutedEventArgs e) {
            if (sender is UIElement element && _resizePointer != null) {
                element.ReleasePointerCapture(_resizePointer);
            }

            _activeResizeSlot = null;
            _resizePointer = null;
            e.Handled = true;
        }

        private void Splitter_PointerCaptureLost(object sender, PointerRoutedEventArgs e) {
            _activeResizeSlot = null;
            _resizePointer = null;
        }

        private void Splitter_PointerEntered(object sender, PointerRoutedEventArgs e) {
            if (_splitterLines.TryGetValue(sender, out var line)) {
                line.Fill = (Brush)Resources[WebBackdropThemeResource.GetBrushKey(WebBackdropBrushRole.SplitterHover)];
            }
        }

        private void Splitter_PointerExited(object sender, PointerRoutedEventArgs e) {
            if (_splitterLines.TryGetValue(sender, out var line)) {
                line.ClearValue(Microsoft.UI.Xaml.Shapes.Shape.FillProperty);
            }
        }

        private void StatusBar_PanelRequested(object? sender, WebEditorBottomPanel panel) {
            var isCurrentPanelVisible = bottomPanel.Visibility == Visibility.Visible && _activeBottomPanel == panel;
            Action updateBottomPanel = isCurrentPanelVisible ? HideBottomPanel : () => ShowBottomPanel(panel);
            updateBottomPanel();
        }

        private void StatusBar_RunRequested(object? sender, RoutedEventArgs e) {
            if (_isDebugRunning) {
                StopDebugSession();
            }
            else {
                SaveAllRequested?.Invoke(this, EventArgs.Empty);
                ViewModel?.DebugProject();
                _isDebugRunning = true;
                statusBar.IsDebugRunning = true;
            }
        }

        private void OnDebugSessionEnded() {
            StopDebugSession();
        }

        private void StopDebugSession() {
            ViewModel?.CleanupSessions();
            _isDebugRunning = false;
            statusBar.IsDebugRunning = false;
        }

        private void StatusBar_DebugRequested(object? sender, RoutedEventArgs e) {
            SaveAllRequested?.Invoke(this, EventArgs.Empty);
            ViewModel?.DebugProject();
        }

        private void StatusBar_ToggleLeftSideBarRequested(object? sender, RoutedEventArgs e) {
            ExecuteCommand(WebEditorCommand.ToggleLeftSideBar);
        }

        private void StatusBar_ToggleBottomPanelRequested(object? sender, RoutedEventArgs e) {
            ExecuteCommand(WebEditorCommand.ToggleBottomPanel);
        }

        private void StatusBar_ToggleRightSideBarRequested(object? sender, RoutedEventArgs e) {
            ExecuteCommand(WebEditorCommand.ToggleRightSideBar);
        }

        private async void StatusBar_IndentChanged(object? sender, (int TabSize, bool InsertSpaces) args) {
            if (ViewModel?.ActiveFile == null) return;

            //IndentText = args.InsertSpaces ? $"Spaces: {args.TabSize}" : $"Tabs: {args.TabSize}";
            var monacoEditor = GetActiveMonacoEditor();
            if (monacoEditor != null) {
                await monacoEditor.SetIndentOptionsAsync(args.TabSize, args.InsertSpaces);
            }
        }

        private async void StatusBar_EncodingChanged(object? sender, string encoding) {
            if (ViewModel?.ActiveFile == null) return;

            //EncodingText = encoding;
            //ViewModel.ActiveFile.SetEncoding(encoding);
            //ViewModel.ActiveFile.ReopenWithEncoding(encoding);

            var monacoEditor = GetActiveMonacoEditor();
            if (monacoEditor != null) {
                await monacoEditor.SetEncodingAsync(encoding);
                monacoEditor.EditorContent = ViewModel.ActiveFile.Content;
            }
        }

        private async void StatusBar_LineEndingChanged(object? sender, string lineEnding) {
            if (ViewModel?.ActiveFile == null) return;

            var monacoEditor = GetActiveMonacoEditor();
            if (monacoEditor != null) {
                await monacoEditor.ReplaceLineEndingsAsync(lineEnding == "LF" ? "\\n" : "\\r\\n");
            }
        }

        private MonacoEditor? GetActiveMonacoEditor() {
            return editorContentView.ActiveMonacoEditor;
        }

        private async void SyncEncodingToMonaco(string encoding) {
            var monacoEditor = GetActiveMonacoEditor();
            if (monacoEditor == null) return;

            await monacoEditor.SetEncodingAsync(encoding);
        }

        private void BottomPanelSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e) {
            if (sender.SelectedItem is SelectorBarItem { Tag: WebEditorBottomPanel panel }) {
                ShowBottomPanel(panel);
            }
        }

        private void BottomPanelClose_Click(object sender, RoutedEventArgs e) {
            HideBottomPanel();
        }

        private void ShowBottomPanel(WebEditorBottomPanel panel) {
            SetBottomPanel(panel);
            SetPanelVisibility(EditorPanelSlot.Bottom, true);
        }

        private void HideBottomPanel() {
            SetPanelVisibility(EditorPanelSlot.Bottom, false);
        }

        private void SetBottomPanel(WebEditorBottomPanel panel) {
            _activeBottomPanel = panel;
            UpdateBottomPanelTabStates();
            var showProblems = panel == WebEditorBottomPanel.Problems && problemsPanel.ProblemCount > 0;
            problemsPanel.Visibility = showProblems ? Visibility.Visible : Visibility.Collapsed;
            bottomPanelContent.Visibility = showProblems ? Visibility.Collapsed : Visibility.Visible;
            bottomPanelContent.Text = panel switch {
                WebEditorBottomPanel.Problems => "当前没有检测到问题。",
                WebEditorBottomPanel.Output => "暂无输出。",
                _ => string.Empty,
            };
        }

        private void UpdateBottomPanelTabStates() {
            foreach (var item in bottomPanelSelectorBar.Items) {
                if (item is SelectorBarItem selectorBarItem && selectorBarItem.Tag is WebEditorBottomPanel panel) {
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
            if (_activeBottomPanel == WebEditorBottomPanel.Problems) {
                SetBottomPanel(_activeBottomPanel);
            }
        }

        /*
         * 编辑器内容每次变化都会触发 ContentChanged。如果每输入一个字符都刷新右侧属性面板，会频繁计算行数、状态、文件信息，浪费
         * 
         * 每次调用都生成新版本，让旧任务失效
         * 等 250ms。期间如果又输入字符，版本会增加
         * 防止等待期间切换文件。只有当前文件还是当初那个文件，才刷新面板
         * 
         * 输入停止 250ms 后，只刷新当前文件一次。避免频繁刷新和旧文件回写
         */
        private void QueuePropertyPanelRefresh(WebEditorFile file, string language) {
            _propertyPanelRefreshCancellation?.Cancel();
            _propertyPanelRefreshCancellation?.Dispose();
            _propertyPanelRefreshCancellation = new CancellationTokenSource();
            _ = RefreshPropertyPanelAsync(file, language, _propertyPanelRefreshCancellation.Token);
        }

        private async Task RefreshPropertyPanelAsync(WebEditorFile file, string language, CancellationToken cancellationToken) {
            try {
                await Task.Delay(250, cancellationToken);
                if (ViewModel?.ActiveFile?.Equals(file) == true) {
                    propertyPanelControl.Load(file, language);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async void ProblemsPanel_ProblemRequested(object? sender, ProblemItem item) {
            await OpenFileAsync(item.FilePath);
            await Task.Delay(50);
            await editorContentView.RevealPositionAsync(item.LineNumber, item.ColumnNumber);
        }

        private void UpdateStatusBarLayoutState() {
            statusBar.IsLeftSideBarVisible = leftSideBar.Visibility == Visibility.Visible;
            statusBar.IsBottomPanelVisible = bottomPanel.Visibility == Visibility.Visible;
            statusBar.IsRightSideBarVisible = rightSideBar.Visibility == Visibility.Visible;
            statusBar.RefreshLayoutState();
        }

        private WebProjectSession? _session;
        private bool _isLoaded;
        private bool _isDebugRunning;
        private CancellationTokenSource? _propertyPanelRefreshCancellation;
        private WebEditorBottomPanel _activeBottomPanel = WebEditorBottomPanel.Problems;
        private string? _activeEditorFilePath;
        private Dictionary<EditorPanelSlot, PanelLayoutState> _panelLayoutStates = null!;
        private Dictionary<object, Microsoft.UI.Xaml.Shapes.Rectangle> _splitterLines = null!;
        private Dictionary<WebEditorCommand, Action> _commandActions = null!;
        private Dictionary<(VirtualKey Key, VirtualKeyModifiers Modifiers), WebEditorCommand> _keyboardCommands = null!;
        private Dictionary<string, WebEditorCommand> _monacoCommands = null!;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private EditorPanelSlot? _activeResizeSlot;
        private Pointer? _resizePointer;
        private double _resizeStartPointerPosition;
        private double _resizeStartSize;
    }

    public enum WebEditorBottomPanel {
        Problems,
        Output,
    }
}
