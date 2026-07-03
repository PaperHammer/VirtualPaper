using System.Windows;
using System.Windows.Input;
using VirtualPaper.Common;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.lang;

namespace VirtualPaper.Views {
    public partial class PluginUpdateWindow : Window {
        public PluginUpdateWindow() {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        public void ReportProgress(PluginsUpdateProgress progress) {
            Dispatcher.Invoke(() => {
                UpdateProgressBar.Value = progress.Percent;
                StatusText.Text = progress.Message;

                if (progress.Stage == PluginsUpdateStage.Completed) {
                    TitleText.Text = LanguageManager.Instance[nameof(Constants.I18n.PluginUpdate_Title_Completed)];
                }
                else if (progress.Stage == PluginsUpdateStage.Failed) {
                    TitleText.Text = LanguageManager.Instance[nameof(Constants.I18n.PluginUpdate_Title_Failed)];
                }
            });
        }

        public void ShowError(string message) {
            Dispatcher.Invoke(() => {
                TitleText.Text = LanguageManager.Instance[nameof(Constants.I18n.PluginUpdate_Title_Failed)];
                StatusText.Text = message;
                UpdateProgressBar.Foreground = System.Windows.Media.Brushes.Red;
            });
        }
    }
}
