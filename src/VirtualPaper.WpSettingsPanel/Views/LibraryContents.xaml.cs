using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;
using Windows.ApplicationModel.DataTransfer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.WpSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LibraryContents : ArcPage {
        public override ArcPageContext Context { get; }
        public override Type PageType => typeof(LibraryContents);

        public LibraryContents() {
            _viewModel = ObjectProvider.GetRequiredService<LibraryContentsViewModel>(lifetimeForParams: ObjectLifetime.Singleton);
            this.DataContext = _viewModel;
            Context = new ArcPageContext(this);
            this.InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            if (!_isColdLaunch) return;

            await _viewModel.InitContentAsync();
            _isColdLaunch = false;
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
            ArcLog.GetLogger<LibraryContents>().Error($"RImage loading failed: {e.ErrorMessage}");
        }

        private async void Item_Tapped(object sender, TappedRoutedEventArgs e) {
            if (((FrameworkElement)sender).DataContext is not IWpBasicData data) return;
            await _viewModel.PreviewAsync(data);
        }

        private async void ContextMenu_Click(object sender, RoutedEventArgs e) {
            try {
                if (((FrameworkElement)sender).DataContext is not IWpBasicData data)
                    return;
                
                var selectedMeun = (MenuFlyoutItem)sender;
                string? name = selectedMeun.Tag.ToString();
                switch (name) {
                    case "Details":
                        _viewModel.ShowDetail(data);
                        break;
                    case "UpdateConfig":
                        await _viewModel.UpdateAsync(data);
                        break;
                    case "Edit":
                        await _viewModel.EditInfoAsync(data);
                        break;
                    case "PreviewForWeb":
                        await _viewModel.PreviewAsync(data);
                        break;
                    case "Apply":
                        await _viewModel.ApplyAsync(data);
                        break;
                    case "LockBackground":
                        await _viewModel.ApplyToLockBGAsync(data);
                        break;
                    case "ShowOnDisk":
                        Process.Start("Explorer", "/select," + data.FilePath);
                        break;
                    case "DeleteFromDisk":
                        await _viewModel.DeleteAsync(data);
                        break;
                }
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
                ArcLog.GetLogger<LibraryContents>().Error(ex);
            }
        }

        private void WallpapersLibView_DragOver(object sender, DragEventArgs e) {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void WallpapersLibView_Drop(object sender, DragEventArgs e) {
            if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
                var items = await e.DataView.GetStorageItemsAsync();
                await _viewModel.DropFilesAsync(items);
            }
            e.Handled = true;
        }

        private void WallpapersLibView_PreviewKeyDown(object sender, KeyRoutedEventArgs e) {
            e.Handled = true;
        }

        private readonly LibraryContentsViewModel _viewModel;
        private bool _isColdLaunch = true;
    }
}
