using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views.Comp {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConfigSpace : ArcPage {
        public override Type ArcType => typeof(ConfigSpace);

        public ConfigSpace() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<ConfigSpaceViewModel>();
            this.DataContext = _viewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is FrameworkPayload payload) {
                payload.TryGet(NaviPayloadKey.SelectedIntelliPageIdx, out _selectedPageIdx);
                Payload = Payload.Merge(payload);
            }
        }

        private void FrameComp_Loaded(object sender, RoutedEventArgs e) {
            NaviAddTaskPage();
        }

        private void NaviAddTaskPage() {
            var targetTaskPageType = _selectedPageIdx switch {
                0 => typeof(StyleTransferComp.StyleTransferAddTask),
                1 => typeof(SuperResolutionComp.SuperResolutionAddTask),
                _ => throw new NotImplementedException(),
            };
            FrameComp.Navigate(targetTaskPageType, Payload);

            if (FrameComp.Content is ICardComponent cardComponent) {
                cardComponent.CardUIStateChanged = () => {
                    _viewModel.RefreshCardComponentData();
                };
                _viewModel._cardComponent = cardComponent;
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            FrameComp.Content = null;
            FrameComp.DataContext = null;
            _viewModel.Dispose();
        }

        private readonly ConfigSpaceViewModel _viewModel;
        private int _selectedPageIdx;
    }
}
