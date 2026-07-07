using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using VirtualPaper.Common.Utils.PInvoke;
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
            Loaded += AppUpdaterWindow_Loaded;
            _viewModel.RequestFlashTaskbar = FlashTaskbar;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            TaskbarItemInfo = new TaskbarItemInfo();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(AppUpdaterWindowViewModel.Progress)
                || e.PropertyName == nameof(AppUpdaterWindowViewModel.CurrentState)) {
                UpdateTaskbarProgress();
            }
        }

        private void UpdateTaskbarProgress() {
            if (TaskbarItemInfo == null) return;

            switch (_viewModel.CurrentState) {
                case DownloadState.Downloading:
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                    TaskbarItemInfo.ProgressValue = _viewModel.Progress / 100.0;
                    break;
                case DownloadState.Verifying:
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
                    TaskbarItemInfo.ProgressValue = 0;
                    break;
                case DownloadState.Completed:
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    TaskbarItemInfo.ProgressValue = 0;
                    break;
                default:
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    TaskbarItemInfo.ProgressValue = 0;
                    break;
            }
        }

        private void AppUpdaterWindow_Loaded(object sender, RoutedEventArgs e) {
            _viewModel?.AutoStartDownload();
        }

        private void FlashTaskbar() {
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            var fi = new Native.FLASHWINFO {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.FLASHWINFO>(),
                hwnd = hwnd,
                dwFlags = Native.FLASHW_TRAY | Native.FLASHW_TIMERNOFG,
                uCount = uint.MaxValue,
                dwTimeout = 0
            };
            Native.FlashWindowEx(ref fi);
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

        private void ActionBtn_Click(object sender, RoutedEventArgs e) {
            if (_viewModel.CurrentState == DownloadState.Completed) {
                this.Close();
                return;
            }

            _viewModel.OnActionCommand();
        }

        private readonly AppUpdaterWindowViewModel _viewModel;
    }
}
