using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using VirtualPaper.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace VirtualPaper.Views {
    /// <summary>
    /// AppUpdaterWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AppUpdaterWindow : FluentWindow {
        public AppUpdaterWindow(
            IContentDialogService contentDialogService,
            AppUpdaterWindowViewModel viewModel) {
            InitializeComponent();
            contentDialogService.SetDialogHost(RootContentDialog);
            DataContext = _viewModel = viewModel;
        }

        private void FluentWindow_Closed(object? sender, EventArgs e) {
            _viewModel?.Dispose();
        }

        /// <summary>
        /// 关闭前检查当前状态，必要时进行拦截
        /// </summary>
        private async void FluentWindow_Closing(object? sender, CancelEventArgs e) {
            switch (_viewModel.CurrentState) {
                case DownloadState.Downloading:
                case DownloadState.Paused:
                case DownloadState.DownloadFailed:
                case DownloadState.VerifyFailed:
                case DownloadState.Verifying:
                case DownloadState.Completed:
                    e.Cancel = true;

                    var confirmClose = await _viewModel.ShowCancelDialogAsync();
                    if (confirmClose) {
                        _viewModel.Cancel();
                        this.Closing -= FluentWindow_Closing;
                        this.Close();
                    }
                    break;
            }
        }

        private void MarkdownScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            scroller.RaiseEvent(eventArg);
        }

        private void FluentWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            if (key == Key.Tab) {
                e.Handled = true;
            }
        }

        private readonly AppUpdaterWindowViewModel _viewModel;
    }
}
