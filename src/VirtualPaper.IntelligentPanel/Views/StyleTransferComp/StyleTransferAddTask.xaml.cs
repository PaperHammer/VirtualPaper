using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views.StyleTransferComp {
    public sealed partial class StyleTransferAddTask : ArcPage, ICardComponent {
        public override Type ArcType => typeof(StyleTransferAddTask);
        public Action? CardUIStateChanged {
            get => _viewModel.CardUIStateChanged;
            set => _viewModel.CardUIStateChanged = value;
        }
        public string PreviousStepBtnText => _viewModel.PreviousStepBtnText;
        public string NextStepBtnText => _viewModel.NextStepBtnText;
        public bool BtnVisible => _viewModel.BtnVisible;
        public bool IsNextEnable => _viewModel.IsNextEnable;
        public Func<object?, Task>? PreviousStepAction => async (_) => await _viewModel.OnPreviousStepClickedAsync();
        public Func<object?, Task>? NextStepAction => async (_) => await _viewModel.OnNextStepClickedAsync();

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

            if (e.Parameter is FrameworkPayload payload) {
                _viewModel.IntelligentCTS = payload.Get<TaskCompletionSource<string[]?>>(NaviPayloadKey.IntelligentCTS);
                _viewModel.UpdateCardComponentUI();
            }
        }

        private async void SourceImageBorder_Tapped(object sender, TappedRoutedEventArgs e) {
            await _viewModel.SelectSourceImageAsync();
        }

        private async void StyleImageBorder_Tapped(object sender, TappedRoutedEventArgs e) {
            await _viewModel.SelectStyleImageAsync();
        }

        private void CleanupImageResources() {
            if (sourceImage != null) {
                sourceImage.Source = null;
            }
            if (styleImage != null) {
                styleImage.Source = null;
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
