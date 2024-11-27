using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
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
            //_viewModel.SelectBarChanged += WpSettingsViewModel_SelectBarChanged;
            _viewModel.UpdateMonitorLayout();
            this.DataContext = _viewModel;
        }

        private void WpSettingsViewModel_SelectBarChanged(object sender, int index) {
            var items = SelBar.Items;
            if (index >= 0 && index < items.Count) {
                SelBar.SelectedItem = items[index];
            }
        }

        #region btn_click
        private async void BtnClose_Click(object sender, RoutedEventArgs e) {
            BtnClose.IsEnabled = false;
            _viewModel.Close();
            await Task.Delay(3000);
            BtnClose.IsEnabled = true;
        }

        //private async void BtnRestore_Click(object sender, RoutedEventArgs e) {
        //    BtnRestore.IsEnabled = false;
        //    _viewModel.Restore();
        //    await Task.Delay(3000);
        //    BtnRestore.IsEnabled = true;
        //}

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
            await _viewModel.PreviewAsync();
        }

        //private async void BtnApply_Click(object sender, RoutedEventArgs e) {
        //    BtnApply.IsEnabled = false;
        //    _viewModel.Apply();
        //    await Task.Delay(3000);
        //    BtnApply.IsEnabled = true;
        //}
        #endregion

        #region nav       
        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) {
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);

            Type pageType = currentSelectedIndex switch {
                0 => typeof(LibraryContents),
                1 => typeof(WpRuntimeSettings),
                _ => null,
            };
            var slideNavigationTransitionEffect = currentSelectedIndex - _previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

            ContentFrame.Navigate(pageType, null, new SlideNavigationTransitionInfo() { Effect = slideNavigationTransitionEffect });

            _previousSelectedIndex = currentSelectedIndex;
        }
        #endregion

        private readonly WpSettingsViewModel _viewModel;
        private int _previousSelectedIndex = 0;
    }
}
