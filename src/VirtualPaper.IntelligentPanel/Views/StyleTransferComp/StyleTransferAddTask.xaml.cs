using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views.StyleTransferComp {
    public sealed partial class StyleTransferAddTask : Page {
        public StyleTransferAddTask() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<StyleTransferAddTaskViewModel>();
            this.DataContext = _viewModel;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            CleanupImageResources();
            CleanupBindings();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
        }

        private void SourceImageBorder_Tapped(object sender, TappedRoutedEventArgs e) {

        }

        private void StyleImageBorder_Tapped(object sender, TappedRoutedEventArgs e) {

        }

        private void StyleGridView_ItemClick(object sender, ItemClickEventArgs e) {

        }

        private void CleanupImageResources() {
            if (sourceImage != null) {
                sourceImage.Source = null;
            }
            if (stylePreviewImage != null) {
                stylePreviewImage.Source = null;
            }
        }

        private void CleanupBindings() {
            this.DataContext = null;

            if (styleGridView != null) {
                styleGridView.ItemsSource = null;
                //styleGridView.SelectedItem = null;
            }
        }

        private readonly StyleTransferAddTaskViewModel _viewModel;
    }
}
