using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.lang;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Views {
    public partial class PluginUpdateWindow : Window {
        public PluginUpdateWindow() {
            InitializeComponent();
            this.Loaded += PluginUpdateWindow_Loaded;

            TaskbarItemInfo = new TaskbarItemInfo();
        }

        private void PluginUpdateWindow_Loaded(object sender, RoutedEventArgs e) {
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            Close();
            _ = App.Services.GetRequiredService<IUIRunnerService>().ShowUIAsync();
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

        public void ReportProgress(PluginsUpdateProgress progress) {
            Dispatcher.Invoke(() => {
                UpdateProgressBar.Value = progress.Percent;
                StatusText.Text = progress.Message;

                if (progress.Stage == PluginsUpdateStage.Completed) {
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    TitleText.Text = LanguageManager.Instance[nameof(Constants.I18n.PluginUpdate_Title_Completed)];
                }
                else if (progress.Stage == PluginsUpdateStage.Failed) {
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    TitleText.Text = LanguageManager.Instance[nameof(Constants.I18n.PluginUpdate_Title_Failed)];
                }
            });
        }

        public void ShowError(string message) {
            Dispatcher.Invoke(() => {
                TitleText.Text = LanguageManager.Instance[nameof(Constants.I18n.PluginUpdate_Title_Failed)];
                StatusText.Text = message;
                UpdateProgressBar.IsIndeterminate = false;
                UpdateProgressBar.Value = 100;
                UpdateProgressBar.Foreground = System.Windows.Media.Brushes.Red;
                CloseButton.IsEnabled = true;
                FlashTaskbar();
            });
        }
    }
}
