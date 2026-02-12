using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;
using VirtualPaper.WpSettingsPanel.Views;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.WpSettingsPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WpSettings : ArcPage {
        public override Type ArcType => typeof(WpSettings);

        public WpSettings() {
            this.InitializeComponent();
            this.Unloaded += WpSettings_Unloaded;
            _viewModel = AppServiceLocator.Services.GetRequiredService<WpSettingsViewModel>();
            this.DataContext = _viewModel;            
            ArcContext.AttachLoadingComponent(this.MainHost.LoadingControlHost);
        }

        private void WpSettings_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            this.DataContext = null;
            this.Unloaded -= WpSettings_Unloaded;
        }

        #region nav
        private void NvLocal_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                Type pageType = args.SelectedItemContainer.Name switch {
                    "Nav_LibraryContents" => typeof(LibraryContents),
                    "Nav_ScreenSaver" => typeof(ScreenSaver),
                    _ => throw new NotImplementedException(),
                };

                ContentFrame.Navigate(pageType, this);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WpSettings>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex, key: ex.Message);
            }
        }
        #endregion

        private void Flyout_Opening(object sender, object e) {
            _viewModel.InitFlyoutData();
        }

        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            if (sender is TextBox tb && tb.Tag is FilterKey fk) {
                _viewModel.OnFilterChanged(fk, tb.Text);
            }
        }

        private readonly WpSettingsViewModel _viewModel;
    }
}
