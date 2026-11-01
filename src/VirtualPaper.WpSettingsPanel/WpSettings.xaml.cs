using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
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
                if (ContentFrame.Content is LibraryContents currentLibrary) {
                    currentLibrary.ExitSelectionMode();
                }

                Type pageType = args.SelectedItemContainer.Name switch {
                    "Nav_LibraryContents" => typeof(LibraryContents),
                    "Nav_ScreenSaver" => typeof(ScreenSaver),
                    _ => throw new NotImplementedException(),
                };

                LibraryToolbar.Visibility = args.SelectedItemContainer.Name == "Nav_LibraryContents"
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                ContentFrame.Navigate(pageType, this);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WpSettings>().Error(ex);
                GlobalMessageUtil.ShowException(ex, key: ex.Message);
            }
        }
        #endregion

        private void Flyout_Opening(object sender, object e) {
            _viewModel.InitFlyoutData();
        }

        private async void ScreenFit_Click(object sender, RoutedEventArgs e) {
            if (sender is not Button button || !button.IsEnabled) return;
            button.IsEnabled = false;
            try {
                var viewModel = ActivatorUtilities.CreateInstance<ScreenFitViewModel>(AppServiceLocator.Services);
                if (_viewModel.SelectedMonitorIndex >= 0 && _viewModel.SelectedMonitorIndex < _viewModel.Monitors.Count) {
                    string deviceId = _viewModel.Monitors[_viewModel.SelectedMonitorIndex].DeviceId;
                    viewModel.SelectedMonitor = viewModel.Monitors.FirstOrDefault(m => m.DeviceId == deviceId) ?? viewModel.SelectedMonitor;
                }
                viewModel.Imported += data => AppServiceLocator.Services.GetRequiredService<LibraryContentsViewModel>().UpdateLib(data);
                var dialog = new ScreenFitDialog(viewModel) { XamlRoot = XamlRoot, RequestedTheme = ActualTheme };
                await dialog.ShowAsync();
                if (dialog.Applied) {
                    int index = _viewModel.Monitors.ToList().FindIndex(m => m.DeviceId == viewModel.SelectedMonitor?.DeviceId);
                    if (index >= 0) _viewModel.SelectedMonitorIndex = index;
                    _viewModel.UpdateAdjustWindowCache();
                }
            }
            catch (Exception ex) { ArcLog.GetLogger<WpSettings>().Error(ex); GlobalMessageUtil.ShowException(ex); }
            finally { button.IsEnabled = true; }
        }

        private void OnFilterChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) {
            if (sender.Tag is FilterKey fk) {
                _viewModel.OnFilterChanged(fk, sender.Text);
            }
        }

        private void FilterBar_SizeChanged(object sender, SizeChangedEventArgs e) {
            tbSearchName.Width = Math.Clamp(
                e.NewSize.Width - LibraryTypeFilter.ActualWidth - LibraryBrowseTools.Spacing, 120, 260);
        }

        private void SelectLibraryItems_Click(object sender, RoutedEventArgs e) {
            if (ContentFrame.Content is LibraryContents library) {
                if (SelectLibraryItems.IsChecked == true) {
                    library.EnterSelectionMode();
                }
                else {
                    library.ExitSelectionMode();
                }
            }
        }

        private void SelectAllLibraryItems_Click(object sender, RoutedEventArgs e) {
            if (ContentFrame.Content is LibraryContents library) {
                library.SelectAllItems();
            }
        }

        private void InvertLibrarySelection_Click(object sender, RoutedEventArgs e) {
            if (ContentFrame.Content is LibraryContents library) {
                library.InvertSelection();
            }
        }

        private async void DeleteSelectedLibraryItems_Click(object sender, RoutedEventArgs e) {
            if (ContentFrame.Content is LibraryContents library) {
                await library.DeleteSelectedItemsAsync();
            }
        }

        private void CancelLibrarySelection_Click(object sender, RoutedEventArgs e) {
            if (ContentFrame.Content is LibraryContents library) {
                library.ExitSelectionMode();
            }
        }

        internal void UpdateLibrarySelectionState(bool isSelecting, int selectedCount) {
            SelectLibraryItems.IsChecked = isSelecting;
            AddToLib.IsEnabled = !isSelecting;
            ScreenFitButton.IsEnabled = !isSelecting;
            LibraryBrowseTools.Visibility = isSelecting ? Visibility.Collapsed : Visibility.Visible;
            LibrarySelectionTools.Visibility = isSelecting ? Visibility.Visible : Visibility.Collapsed;
            LibrarySelectionCount.Text = string.Format(
                LanguageUtil.GetI18n(Constants.I18n.WpLib_SelectedCount),
                selectedCount);
            DeleteSelectedLibraryItems.IsEnabled = selectedCount > 0;
        }

        private readonly WpSettingsViewModel _viewModel;
    }
}
