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

namespace VirtualPaper.IntelligentPanel.Views.SuperResolutionComp {
    public sealed partial class SuperResolutionAddTask : ArcPage, ICardComponent, IIntelligentAddTask {
        public override Type ArcType => typeof(SuperResolutionAddTask);
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

        public SuperResolutionAddTask() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<SuperResolutionAddTaskViewModel>();
            this.DataContext = _viewModel;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            CleanupImageResources();
            CleanupBindings();
            CleanViewModel();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is FrameworkPayload payload) {
                _viewModel.IntelligentCTS = payload.Get<ResettableCompletionSource<IIntelliData?>>(NaviPayloadKey.IntelligentCTS);
            }
        }

        private async void SourceImageBorder_Tapped(object sender, TappedRoutedEventArgs e) {
            await _viewModel.SelectSourceImageAsync();
        }

        private void EnhanceMode_Changed(object sender, RoutedEventArgs e) {
            // RadioButton 的 IsChecked 通过 x:Bind TwoWay 已自动同步到 ViewModel
            // 这里可以做额外 UI 动画等，如果不需要可以留空或移除
        }

        private void Magnification_Changed(object sender, RoutedEventArgs e) {
            // 同上，通过 x:Bind TwoWay 已处理
        }

        private void CleanupImageResources() {
            if (sourceImage != null) {
                sourceImage.Source = null;
            }
        }

        private void CleanupBindings() {
            this.DataContext = null;
        }

        private void CleanViewModel() {
            _viewModel.Clean();
        }

        public void UpdateCardComponentUI() {
            _viewModel.UpdateCardComponentUI();
        }

        public void ClearAddTask() {
            CleanViewModel();
            UpdateCardComponentUI();
        }

        private readonly SuperResolutionAddTaskViewModel _viewModel;
    }
}