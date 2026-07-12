using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Logging;
using VirtualPaper.Models.Cores;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.ViewModels;
using Workloads.Creation.WebBackdrop.Views.Tools;

namespace Workloads.Creation.WebBackdrop.Views.Components {
    public sealed partial class WebEditor : ArcUserControl {
        public FrameworkPayload? Payload { get; set; }

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

        private WebProjectSession? _session;
        private WebFileTreeControl? _fileTreeControl;
        private WebProjectInfoControl? _projectInfoControl;

        public WebEditor() {
            InitializeComponent();
        }

        private void ArcUserControl_Loaded(object sender, RoutedEventArgs e) {
            if (Payload == null) return;

            Payload.TryGet(NaviPayloadKey.WebProjectSession, out _session);
            if (_session == null) return;

            ViewModel = new WebEditorViewModel(_session);
            ViewModel.OpenFiles.CollectionChanged += OpenFiles_CollectionChanged;

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

        private void UpdateStatusBar() {
            if (ViewModel?.ActiveFile != null) {
                ActiveFilePathText = ViewModel.ActiveFile.FilePath;
                ActiveFileLanguage = GetLanguageFromExtension(ViewModel.ActiveFile.FileExtension);
            }
            else {
                ActiveFilePathText = string.Empty;
                ActiveFileLanguage = "plaintext";
            }
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

        private void ToolList_ToolListLoaded(object? sender, RoutedEventArgs e) {
            toolPanel.Visibility = Visibility.Visible;
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

        private void ProjectInfo_ProjectInfoSaved(object? sender, WpWebProjectData data) {
            _session?.DesignFileUtil.SaveProjectData(data);
        }

        private void StatusBar_PreviewRequested(object? sender, RoutedEventArgs e) {
            // TODO: Open preview window
            ArcLog.GetLogger<WebEditor>().Info("Preview requested");
        }
    }
}
