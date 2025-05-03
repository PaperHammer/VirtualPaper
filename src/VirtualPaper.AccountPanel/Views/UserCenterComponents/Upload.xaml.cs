using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AccountPanel.ViewModels;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores.Interfaces;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AccountPanel.Views.UserCenterComponents {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Upload : Page {
        public Upload() {
            this.InitializeComponent();
        }

        public Upload(IWpBasicData data) : this() {
            _viewModel = ObjectProvider.GetRequiredService<UploadViewModel>(lifetimeForParams: ObjectLifetime.Singleton);
            _viewModel.WpBasicData = data;
            _viewModel.FillData();
            this.DataContext = _viewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            this._accountPanel = e.Parameter as IAccountPanelBridge;
            _viewModel = ObjectProvider.GetRequiredService<UploadViewModel>(lifetimeForParams: ObjectLifetime.Singleton);
            this.DataContext = _viewModel;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            await _viewModel.InitPartitionsAsync();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e) {
            var storage = await WindowsStoragePickers.PickFilesAsync(
                Account.Instance.GetWindowHandle(),
                FileFilter.FileTypeToExtension[FileType.FImage]
                    .Concat(FileFilter.FileTypeToExtension[FileType.FGif])
                    .Concat(FileFilter.FileTypeToExtension[FileType.FVideo])
                    .ToArray());
            if (storage.Length < 1) return;
            await _viewModel.TryImportAsync(storage[0].Path);
        }

        private void Txbtags_KeyDown(object sender, KeyRoutedEventArgs e) {
            if (e.Key == VirtualKey.Enter) {
                string tagText = txbTags.Text.TrimStart().TrimEnd();
                if (tagText.Length == 0 || _viewModel.TagList.Count >= 7 || _viewModel.TagList.Contains(tagText)) {
                    return;
                }
                txbTags.Text = string.Empty;
                _viewModel.TagList.Add(tagText);
            }
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e) {
            await _viewModel.UploadWallpaperAsync();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e) {
            _viewModel.Clear();
        }

        private void TagDelButton_Click(object sender, RoutedEventArgs e) {
            var button = sender as Button;
            if (button != null) {
                var stackPanel = button.Parent as StackPanel;
                if (stackPanel != null) {
                    foreach (var child in stackPanel.Children) {
                        var textBlock = child as TextBlock;
                        if (textBlock != null) {
                            string tagText = textBlock.Text;
                            _viewModel.TagList.Remove(tagText);
                            break;
                        }
                    }
                }
            }
        }

        private UploadViewModel _viewModel;
        private IAccountPanelBridge _accountPanel;
    }
}
