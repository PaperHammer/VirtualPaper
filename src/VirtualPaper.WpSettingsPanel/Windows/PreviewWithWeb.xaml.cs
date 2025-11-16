using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.WpSettingsPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.WpSettingsPanel.Windows {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PreviewWithWeb : ArcWindow {
        public override ArcWindowHost ContentHost => throw new System.NotImplementedException();

        public PreviewWithWeb(
            PreviewWithWebViewModel viewModel,
            IUserSettingsClient userSettings) : base(userSettings.Settings.ApplicationTheme, userSettings.Settings.SystemBackdrop) {
            this.InitializeComponent();

            _userSettings = userSettings;
            _viewModel = viewModel;
            this.ContentHost.AppRoot.DataContext = _viewModel;
        }

        private void Webview2_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
            e.Handled = true; // 阻止（鼠标等）指针操作
        }

        private void Webview2_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) {
            e.Handled = true;  // 阻止键盘操作
        }

        private readonly IUserSettingsClient _userSettings;
        internal readonly PreviewWithWebViewModel _viewModel;
    }
}
