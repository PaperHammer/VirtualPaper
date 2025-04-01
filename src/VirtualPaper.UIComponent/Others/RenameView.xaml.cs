using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Others {
    public sealed partial class RenameView : UserControl {
        public RenameView(RenameViewModel viewModel) {
            this.InitializeComponent();

            this._viewModel = viewModel;
        }

        private RenameViewModel _viewModel;
    }
}
