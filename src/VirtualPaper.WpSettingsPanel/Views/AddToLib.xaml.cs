using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.WpSettingsPanel.ViewModels;
using Windows.ApplicationModel.DataTransfer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.WpSettingsPanel.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AddToLib : Page {
    public AddToLib(AddToLibViewModel viewModel) {
        InitializeComponent();
        this._viewModel = viewModel;
    }

    private async void Page_Drop(object sender, DragEventArgs e) {
        this.AddPanel.Visibility = Visibility.Visible;
        this.AddPanelDrop.Visibility = Visibility.Collapsed;

        if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
            var items = await e.DataView.GetStorageItemsAsync();
            _viewModel.AddWallpaperFiles(items);
        }
    }

    private void Page_DragOver(object sender, DragEventArgs e) {
        e.AcceptedOperation = DataPackageOperation.Copy;
        this.AddPanel.Visibility = Visibility.Collapsed;
        this.AddPanelDrop.Visibility = Visibility.Visible;
    }

    private void Page_DragLeave(object sender, DragEventArgs e) {
        this.AddPanel.Visibility = Visibility.Visible;
        this.AddPanelDrop.Visibility = Visibility.Collapsed;
    }

    private readonly AddToLibViewModel _viewModel;
}
