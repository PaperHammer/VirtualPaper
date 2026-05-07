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

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
        }

        private void SourceImageBorder_Tapped(object sender, TappedRoutedEventArgs e) {

        }

        private void StyleImageBorder_Tapped(object sender, TappedRoutedEventArgs e) {

        }

        private void StyleGridView_ItemClick(object sender, ItemClickEventArgs e) {

        }

        private readonly StyleTransferAddTaskViewModel _viewModel;
    }
}
