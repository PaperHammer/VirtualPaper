using System.ComponentModel;
using VirtualPaper.Utils.Interfcaes;
using VirtualPaper.ViewModels;
using Wpf.Ui.Controls;

namespace VirtualPaper.Views {
    public partial class RestartUpdateWindow : FluentWindow {
        public RestartUpdateWindow(ReleaseInfo releaseInfo) {
            InitializeComponent();
            _viewModel = new RestartUpdateWindowViewModel();
            DataContext = _viewModel;

            _viewModel.CloseRequested += () => Close();

            Loaded += async (s, e) => {
                await _viewModel.StartUpdateAsync(releaseInfo);
            };
        }

        protected override void OnClosing(CancelEventArgs e) {
            // Prevent closing while update is in progress
            if (!_viewModel.IsCompleted && !_viewModel.HasError) {
                e.Cancel = true;
            }
            base.OnClosing(e);
        }
        
        private readonly RestartUpdateWindowViewModel _viewModel;
    }
}
