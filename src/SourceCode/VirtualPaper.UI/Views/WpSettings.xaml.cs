using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UI.Views.WpSettingsComponents;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WpSettings : Page {
        public WpSettings() {
            this.InitializeComponent();

            _viewModel = App.Services.GetRequiredService<WpSettingsViewModel>();
            this.DataContext = _viewModel;
        }

        #region btn_click
        private async void BtnClose_Click(object sender, RoutedEventArgs e) {
            BtnClose.IsEnabled = false;
            _viewModel.Close();
            await Task.Delay(3000);
            BtnClose.IsEnabled = true;
        }

        private async void BtnDetect_Click(object sender, RoutedEventArgs e) {
            BtnDetect.IsEnabled = false;
            await _viewModel.DetectAsync();
            await Task.Delay(3000);
            BtnDetect.IsEnabled = true;
        }

        private async void BtnIdentify_Click(object sender, RoutedEventArgs e) {
            BtnIdentify.IsEnabled = false;
            await _viewModel.IdentifyAsync();
            await Task.Delay(3000);
            BtnIdentify.IsEnabled = true;
        }

        private async void BtnAdjust_Click(object sender, RoutedEventArgs e) {
            await _viewModel.AdjustAsync();
        }

        private async void BtnPreview_Click(object sender, RoutedEventArgs e) {
            await _viewModel.PreviewAsync();
        }
        #endregion

        #region nav
        private void NvLocal_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                FrameNavigationOptions navOptions = new() {
                    TransitionInfoOverride = args.RecommendedNavigationTransitionInfo,
                    IsNavigationStackEnabled = false
                };

                Type pageType = null;
                if (args.SelectedItemContainer.Name == LibraryContents.Name) {
                    pageType = typeof(LibraryContents);
                }
                else if (args.SelectedItemContainer.Name == WpRuntimeSettings.Name) {
                    pageType = typeof(WpRuntimeSettings);
                }

                ContentFrame.NavigateToType(pageType, null, navOptions);
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
                App.Log.Error(ex);
            }
        }
        #endregion

        private readonly WpSettingsViewModel _viewModel;

        private void WpArrageRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e) {

        }
    }
}
