using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AccountPanel.ViewModels;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Models.Cores.Interfaces;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AccountPanel.Views.UserCenterComponents {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CloudLib : Page {
        public CloudLib() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            this._accountPanel = e.Parameter as IAccountPanelBridge;

            _viewModel = ObjectProvider.GetRequiredService<CloudLibViewModel>(lifetimeForParams: ObjectLifetime.Singleton);
            this.DataContext = _viewModel;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            await _viewModel.InitContentAsync();
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
            this._accountPanel.Log(LogType.Error, $"RImage loading failed: {e.ErrorMessage}");
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e) {
            _data = e.ClickedItem as IWpBasicData;
            LeftClick();
        }

        private void GridView_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            var dataContext = ((FrameworkElement)e.OriginalSource).DataContext;
            _data = dataContext as IWpBasicData;
            RightClick(sender, e);
        }

        private async void LeftClick() {
            if (_data == null) return;
            await _viewModel.PreviewAsync(_data);
        }

        private void RightClick(object sender, RightTappedRoutedEventArgs e) {
            if (_data == null) {
                // Hide() 方法可能无效是因为 MenuFlyout 是由 ContextFlyout 属性触发
                // ItemsViewMenu.Hide();
                wallpapersLibView.ContextFlyout = null;
            }
            else {
                wallpapersLibView.ContextFlyout = ItemsViewMenu;
            }
        }

        private async void ContextMenu_Click(object sender, RoutedEventArgs e) {
            if (_data == null) return;

            try {
                var selectedMeun = (MenuFlyoutItem)sender;
                string name = selectedMeun.Name;

                switch (name) {
                    case "DetailsAndEdit":
                        await _viewModel.DetailAndEditInfoAsync(_data);
                        break;
                    case "Preview":
                        await _viewModel.PreviewAsync(_data);
                        break;
                    case "Download":
                        await _viewModel.DownloadAsync(_data);
                        break;
                    case "DeleteFromCloud":
                        await _viewModel.DeleteAsync(_data);
                        break;
                }
            }
            catch (Exception ex) {
                this._accountPanel.GetNotify().ShowExp(ex);
                this._accountPanel.Log(LogType.Error, ex);
            }
        }

        private void ItemsViewer_PreviewKeyDown(object sender, KeyRoutedEventArgs e) {
            e.Handled = true;
        }

        private IAccountPanelBridge _accountPanel;
        private CloudLibViewModel _viewModel;
        private IWpBasicData _data;
    }
}
