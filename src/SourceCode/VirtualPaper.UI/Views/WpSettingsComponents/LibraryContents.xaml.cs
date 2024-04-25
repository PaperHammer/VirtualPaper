using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NLog;
using System;
using System.Diagnostics;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.UI.ViewModels.WpSettingsComponents;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views.WpSettingsComponents
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LibraryContents : Page
    {
        public LibraryContents()
        {
            this.InitializeComponent();

            _viewModel = App.Services.GetRequiredService<LibraryContentsViewModel>();
            this.DataContext = _viewModel;
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _logger.Error($"Image loading failed: {e.ErrorMessage}");
        }

        private async void ItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
        {
            var md = (IMetaData)args.InvokedItem;
            await _viewModel.Preview(md.FolderPath, this.XamlRoot);
        }

        private void ItemsView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)e.OriginalSource).DataContext;
            _rightTrappedItem = dataContext as IMetaData;

            if (_rightTrappedItem == null)
            {
                //Hide()方法可能无效是因为MenuFlyout是由ContextFlyout属性触发
                //ItemsViewMenu.Hide();
                var itemsView = (ItemsView)sender;
                itemsView.ContextFlyout = null;
            }
            else
            {
                ItemsView.ContextFlyout = ItemsViewMenu;
            }
        }

        private async void ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_rightTrappedItem == null)
            {
                _viewModel.ShowErr(this.XamlRoot);
                return;
            }

            try
            {
                var selectedMeun = (MenuFlyoutItem)sender;
                string name = selectedMeun.Name;

                switch (name)
                {
                    case "DetailedInfo":
                        _viewModel.DetailedInfo(_rightTrappedItem, this.XamlRoot);
                        break;
                    case "EditInfo":
                        _viewModel.EditInfo(_rightTrappedItem, this.XamlRoot);
                        break;
                    case "Preview":
                        await _viewModel.PreviewAsync(_rightTrappedItem);
                        break;
                    case "Import":
                        await _viewModel.ImportAsync(_rightTrappedItem.FilePath);
                        break;
                    case "Apply":
                        await _viewModel.ImportAsync(_rightTrappedItem.FilePath);
                        await _viewModel.ApplyAsync(this.XamlRoot);
                        break;
                    case "ShowOnDisk":
                        Process.Start("Explorer", "/select," + _rightTrappedItem.FilePath);
                        break;
                    case "Delete":
                        await _viewModel.DeleteAsync(_rightTrappedItem, this.XamlRoot);
                        break;
                }
            }
            catch (Exception ex)
            {
                _viewModel.ShowErr(this.XamlRoot);
                _logger.Error(ex);
            }
        }

        private LibraryContentsViewModel _viewModel;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private IMetaData _rightTrappedItem;
    }
}
