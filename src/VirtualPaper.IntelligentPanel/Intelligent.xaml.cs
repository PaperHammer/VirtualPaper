using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.Utils.Interfaces;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.IntelligentPanel.Views;
using VirtualPaper.IntelligentPanel.Views.Comp;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Intelligent : ArcPage, ICardComponent {
        public override Type ArcType => typeof(Intelligent);
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

        public Intelligent() {
            InitializeComponent();
            Payload = new FrameworkPayload() {
                [NaviPayloadKey.ArcPageContext] = this.ArcContext,
                [NaviPayloadKey.ICardComponent] = this,
            };
            _viewModel = AppServiceLocator.Services.GetRequiredService<IntelligentViewModel>();
            this.DataContext = _viewModel;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e) {
            base.OnNavigatedFrom(e);
            CleanupResources();
        }

        private void CleanupResources() {
            // 清理 ContentFrame
            if (ContentFrame.Content is FrameworkElement content) {
                content.DataContext = null;
            }

            // 清理 overlayFrame
            if (overlayFrame.Content is FrameworkElement overlayContent) {
                overlayContent.DataContext = null;
            }

            // 清理 TaskCompletionSource
            _viewModel._intelligentTCS?.TrySetCanceled();
            _viewModel._intelligentTCS = null;

            // 清理页面引用
            _viewModel.SelectedIntelliPage = null;
        }

        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs _) {
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);

            Type pageType = currentSelectedIndex switch {
                0 => typeof(StyleTranfer),
                1 => typeof(SuperResolution),
                _ => throw new NotImplementedException(),
            };
            Payload?.Set(NaviPayloadKey.SelectedIntelliPageIdx, currentSelectedIndex);
            var slideNavigationTransitionEffect = currentSelectedIndex - _previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

            ContentFrame.Navigate(pageType, Payload, new SlideNavigationTransitionInfo() { Effect = slideNavigationTransitionEffect });

            _previousSelectedIndex = currentSelectedIndex;
            _viewModel.SelectedIntelliPage = ContentFrame.Content as IIntelligentPage;
        }

        #region overlay page
        private async void AddTaskButton_Click(object sender, RoutedEventArgs e) {
            _viewModel._intelligentTCS = new TaskCompletionSource<string[]?>();
            ShowOverlayPage(typeof(ConfigSpace), Payload);

            try {
                _viewModel.UpdateCardComponentUI();
                var result = await _viewModel._intelligentTCS.Task;
                _viewModel.AddTask(result);
                HideOverlayPage();
            }
            catch (Exception ex) {
                HideOverlayPage();
                ArcLog.GetLogger<Intelligent>().Error(ex);
            }
        }

        public void ShowOverlayPage(Type pageType, object? parameter) {
            maskGrid.Visibility = Visibility.Visible;
            overlayFrame.Navigate(pageType, parameter);
        }

        public async void HideOverlayPage() {
            // 清理页面数据上下文
            if (overlayFrame.Content is FrameworkElement element) {
                element.DataContext = null;
            }

            // 清理 Frame
            overlayFrame.Content = null;
            overlayFrame.DataContext = null;
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
