using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.Utils.Interfaces;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.IntelligentPanel.Views;
using VirtualPaper.IntelligentPanel.Views.Comp;
using VirtualPaper.UIComponent.Templates;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Intelligent : ArcPage {
        public override Type ArcType => typeof(Intelligent);

        public Intelligent() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<IntelligentViewModel>();
            this.DataContext = _viewModel;
        }

        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs _) {
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);

            Type pageType = currentSelectedIndex switch {
                0 => typeof(StyleTranfer),
                1 => typeof(SuperResolution),
                _ => throw new NotImplementedException(),
            };
            var slideNavigationTransitionEffect = currentSelectedIndex - _previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

            ContentFrame.Navigate(pageType, Payload, new SlideNavigationTransitionInfo() { Effect = slideNavigationTransitionEffect });

            _previousSelectedIndex = currentSelectedIndex;
            _viewModel.SelectedIntelliPage = ContentFrame.Content as IIntelligentPage;
        }

        #region overlay page
        private void AddTaskButton_Click(object sender, RoutedEventArgs e) {
            ShowOverlayPage(typeof(AddTaskView), null);
        }

        public void ShowOverlayPage(Type pageType, object? parameter) {
            maskGrid.Visibility = Visibility.Visible;
            overlayFrame.Navigate(pageType, parameter);
        }

        public async void HideOverlayPage() {
            overlayFrame.Content = null;
            overlayFrame.BackStack.Clear();
            overlayFrame.ForwardStack.Clear();
            maskGrid.Visibility = Visibility.Collapsed;
        }

        private void MaskGrid_Tapped(object sender, TappedRoutedEventArgs e) {
            HideOverlayPage();
        }

        private void OverlayFrame_Tapped(object sender, TappedRoutedEventArgs e) {
            e.Handled = true;
        }
        #endregion

        private int _previousSelectedIndex = 0;
        private readonly IntelligentViewModel _viewModel;
    }
}
