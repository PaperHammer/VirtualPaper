using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.DI;
using Workloads.Creation.WebBackdrop.Core.Theme;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;
using Workloads.Creation.WebBackdrop.ViewModels;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebPropertyPanelControl : UserControl {
        private bool IsLightTheme => ActualTheme == ElementTheme.Light;

        public WebPropertyPanelControl() {
            _viewModel = AppServiceLocator.Services.GetRequiredService<WebPropertyPanelViewModel>();
            DataContext = _viewModel;
            InitializeComponent();

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Loaded += WebPropertyPanelControl_Loaded;
            ActualThemeChanged += WebPropertyPanelControl_ActualThemeChanged;
            UpdatePreviewTheme();
            UpdatePreviewBackground();
        }

        private void WebPropertyPanelControl_Loaded(object sender, RoutedEventArgs e) {
            UpdatePreviewBackground();
            UpdatePreviewHeight();
            if (_viewModel.PreviewHtml != null) {
                _ = NavigatePreviewAsync(_viewModel.PreviewHtml);
            }
        }

        private void WebPropertyPanelControl_ActualThemeChanged(FrameworkElement sender, object args) {
            UpdatePreviewBackground();
            UpdatePreviewTheme();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(WebPropertyPanelViewModel.PreviewHtml)
                && _viewModel.PreviewHtml != null) {
                NavigatePreview(_viewModel.PreviewHtml);
            }
        }

        private void UpdatePreviewBackground() {
            var role = IsLightTheme
                ? WebBackdropColorRole.WebViewLightBackground
                : WebBackdropColorRole.WebViewDarkBackground;
            previewWebView.DefaultBackgroundColor = WebBackdropThemeResource.GetColor(this, role);
        }

        private void UpdatePreviewTheme() {
            _viewModel.SetPreviewTheme(new WebPropertyPanelPreviewTheme(
                GetPreviewString(WebBackdropStringRole.PreviewLightBackground, WebBackdropStringRole.PreviewDarkBackground),
                GetPreviewString(WebBackdropStringRole.PreviewLightForeground, WebBackdropStringRole.PreviewDarkForeground),
                GetPreviewString(WebBackdropStringRole.PreviewLightSecondaryForeground, WebBackdropStringRole.PreviewDarkSecondaryForeground),
                GetPreviewString(WebBackdropStringRole.PreviewLightCodeBackground, WebBackdropStringRole.PreviewDarkCodeBackground),
                GetPreviewString(WebBackdropStringRole.PreviewLightQuoteBorder, WebBackdropStringRole.PreviewDarkQuoteBorder),
                GetPreviewString(WebBackdropStringRole.PreviewLightLinkForeground, WebBackdropStringRole.PreviewDarkLinkForeground)));
        }

        private string GetPreviewString(WebBackdropStringRole lightRole, WebBackdropStringRole darkRole) {
            return WebBackdropThemeResource.GetString(this, IsLightTheme ? lightRole : darkRole);
        }

        private void PreviewWebViewHost_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePreviewHeight(e.NewSize.Width);

        private void UpdatePreviewHeight(double width = 0) {
            var previewWidth = width > 0 ? width : PreviewWebViewHost.ActualWidth;
            if (previewWidth <= 0) return;

            PreviewWebViewHost.Height = Math.Max(160, previewWidth * 10 / 16);
        }

        public void LoadProject(WebDesignFileUtil designFileUtil) {
            _viewModel.LoadProject(designFileUtil);
        }

        public void Load(WebEditorFile? file, string language) {
            UpdatePreviewHeight();
            _viewModel.Load(file, language);
        }

        public void LoadFolder(string folderPath) {
            UpdatePreviewHeight();
            _viewModel.LoadFolder(folderPath);
        }

        public void Clear() {
            UpdatePreviewHeight();
            _viewModel.Clear();
        }

        private async void NavigatePreview(string html) {
            _pendingPreviewHtml = html;
            if (!IsLoaded) return;

            await NavigatePreviewAsync(html);
        }

        private async Task NavigatePreviewAsync(string html) {
            await previewWebView.EnsureCoreWebView2Async();
            if (_pendingPreviewHtml == html) {
                previewWebView.NavigateToString(html);
            }
        }

        private readonly WebPropertyPanelViewModel _viewModel;
        private string? _pendingPreviewHtml;
    }
}
