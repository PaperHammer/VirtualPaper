using System;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.Utils.Interfaces;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.UIComponent.Templates;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StyleTranfer : ArcPage, IIntelligentPage {
        public override Type ArcType => typeof(StyleTranfer);

        public StyleTranfer() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<StyleTranferViewModel>();
            this.DataContext = _viewModel;
        }

        public bool AddTask(IIntelliData data) {
            if (data is StyleTransferInput input) {
                return _viewModel.AddTask(input);
            }

            return false;
        }

        private readonly StyleTranferViewModel _viewModel;
    }
}
