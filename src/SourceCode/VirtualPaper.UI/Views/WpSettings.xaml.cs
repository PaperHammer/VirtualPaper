using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using NLog;
using System;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UI.Views.WpSettingsComponents;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WpSettings : Page
    {
        public WpSettings()
        {
            this.InitializeComponent();

            _viewModel = App.Services.GetRequiredService<WpSettingsViewModel>();
            _viewModel.ResetNavDefault = NavToDefault;
            _viewModel.NavMenuItems = [];
            foreach (var item in NavView.MenuItems)
            {
                _viewModel.NavMenuItems.Add((NavigationViewItem)item);
            }
            this.DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.InitUpdateLayout();
        }

        #region btn_click
        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            BtnClose.IsEnabled = false;

            _viewModel.Close();

            await Task.Delay(3000);
            BtnClose.IsEnabled = true;
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            BtnRestore.IsEnabled = false;

            await _viewModel.RestoreAsync();

            await Task.Delay(3000);
            BtnRestore.IsEnabled = true;
        }

        private async void BtnDetect_Click(object sender, RoutedEventArgs e)
        {
            BtnDetect.IsEnabled = false;

            await _viewModel.DetectAsync(this.XamlRoot);

            await Task.Delay(3000);
            BtnDetect.IsEnabled = true;
        }

        private async void BtnIdentify_Click(object sender, RoutedEventArgs e)
        {
            BtnIdentify.IsEnabled = false;

            await _viewModel.IdentifyAsync();

            await Task.Delay(3000);
            BtnIdentify.IsEnabled = true;
        }

        private async void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.PreviewAsync();
        }

        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            BtnApply.IsEnabled = false;

            await _viewModel.ApplyAsync(this.XamlRoot);

            await Task.Delay(3000);
            BtnApply.IsEnabled = true;
        }
        #endregion

        private void NavToDefault()
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        // ref: https://learn.microsoft.com/zh-cn/windows/apps/design/controls/navigationview#backwards-navigation
        private void NavView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;

            // NavView doesn't load any page by default, so load home page.
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void NavigationView_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected == true)
            {
                NavView_Navigate(typeof(WpNavSettings), args.RecommendedNavigationTransitionInfo);
            }
            else if (args.SelectedItemContainer != null)
            {
                Type navPageType = Type.GetType(args.SelectedItemContainer.Tag.ToString());
                NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
            }
        }

        private void NavView_Navigate(
            Type navPageType,
            NavigationTransitionInfo transitionInfo)
        {
            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            Type preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (navPageType is not null && !Type.Equals(preNavPageType, navPageType))
            {
                ContentFrame.Navigate(navPageType, null, transitionInfo);
            }
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            try
            {
                if (ContentFrame.SourcePageType == typeof(WpNavSettings))
                {
                    // SettingsItem is not part of NavView.NavMenuItems, and doesn't have a Tag.
                    NavView.SelectedItem = (NavigationViewItem)NavView.SettingsItem;
                }
                else if (ContentFrame.SourcePageType != null)
                {
                    // Select the nav view item that corresponds to the page being navigated to.
                    var item =
                        NavView.MenuItems
                        .OfType<NavigationViewItem>()
                        .FirstOrDefault(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()));

                    NavView.SelectedItem = item;
                    if (ContentFrame.Content is WpConfig wpConfig)
                        _viewModel.WpConfigView = wpConfig;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private WpSettingsViewModel _viewModel;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
