using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.Utils.Interfaces;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.IntelligentPanel.Views.DynamicImageComp {
    public sealed partial class DynamicImageAddTask : ArcPage, ICardComponent, IIntelligentAddTask {
        public override Type ArcType => typeof(DynamicImageAddTask);
        public Action? CardUIStateChanged { get => _viewModel.CardUIStateChanged; set => _viewModel.CardUIStateChanged = value; }
        public string PreviousStepBtnText => _viewModel.PreviousStepBtnText;
        public string NextStepBtnText => _viewModel.NextStepBtnText;
        public bool BtnVisible => _viewModel.BtnVisible;
        public bool IsNextEnable => _viewModel.IsNextEnable;
        public Func<object?, Task>? PreviousStepAction => _ => _viewModel.OnPreviousStepClickedAsync();
        public Func<object?, Task>? NextStepAction => _ => _viewModel.OnNextStepClickedAsync();

        public DynamicImageAddTask() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<DynamicImageAddTaskViewModel>();
            DataContext = _viewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            if (e.Parameter is FrameworkPayload payload) {
                _viewModel.IntelligentCTS = payload.Get<ResettableCompletionSource<IIntelliData?>>(
                    NaviPayloadKey.IntelligentCTS);
            }
        }

        private async void SourceImageBorder_Tapped(object sender, TappedRoutedEventArgs e) =>
            await _viewModel.SelectSourceImageAsync();

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            SourceImage.Source = null;
            DataContext = null;
            _viewModel.Clean();
        }

        public void UpdateCardComponentUI() => _viewModel.UpdateCardComponentUI();
        public void ClearAddTask() {
            _viewModel.Clean();
            _viewModel.UpdateCardComponentUI();
        }

        private readonly DynamicImageAddTaskViewModel _viewModel;
    }
}
