using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.Utils.Interfaces;
using VirtualPaper.IntelligentPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StyleTranfer : Page, IIntelligentPage {
        public StyleTranfer() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<StyleTranferViewModel>();
            this.DataContext = _viewModel;
        }

        public void AddTask() {
            _viewModel.AddTask();
        }        

        private readonly StyleTranferViewModel _viewModel;
    }
}
