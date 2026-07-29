using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.ViewModels;

namespace VirtualPaper.UIComponent.Others {
    public sealed partial class AddFileItemView : UserControl {
        public AddFileItemView(AddFileItemViewModel viewModel) {
            this.InitializeComponent();

            this._viewModel = viewModel;
        }

        private readonly AddFileItemViewModel _viewModel;
    }
}
