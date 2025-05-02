using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.GalleryPanel.ViewModels;
using VirtualPaper.Models.Cores.Interfaces;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.GalleryPanel.Views
{
    public sealed partial class WallpaperLib : Page
    {
        public WallpaperLib()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            this._galleryPanel = e.Parameter as IGalleryPanelBridge;
            _viewModel = ObjectProvider.GetRequiredService<WallpaperLibViewModel>(lifetimeForParams: ObjectLifetime.Singleton);
            this.DataContext = _viewModel;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            await _viewModel.InitContentAsync();
        }

        private async void AsbGallery_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) {
            await _viewModel.SearchContentAsync(args.QueryText);
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
            this._galleryPanel.Log(LogType.Error, $"RImage loading failed: {e.ErrorMessage}");
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e) {
            _data = e.ClickedItem as IWpBasicData;
            LeftClick();
        }

        private async void LeftClick() {
            if (_data == null) return;
            await _viewModel.PreviewAsync(_data);
        }

        private void ItemsViewer_PreviewKeyDown(object sender, KeyRoutedEventArgs e) {
            e.Handled = true;
        }

        private IGalleryPanelBridge _galleryPanel;
        private WallpaperLibViewModel _viewModel;
        private IWpBasicData _data;
    }
}
