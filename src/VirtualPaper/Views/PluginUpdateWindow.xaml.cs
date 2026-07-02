using System.Windows;
using VirtualPaper.Cores.AppUpdate;
using Wpf.Ui.Controls;

namespace VirtualPaper.Views {
    public partial class PluginUpdateWindow : FluentWindow {
        public PluginUpdateWindow() {
            InitializeComponent();
        }

        private readonly System.Text.StringBuilder _detailLog = new();

        public void ReportProgress(RestartUpdateProgress progress) {
            Dispatcher.Invoke(() => {
                UpdateProgressBar.Value = progress.Percent;
                StatusText.Text = progress.Message;

                // Append to detail log
                if (_detailLog.Length > 0) _detailLog.AppendLine();
                _detailLog.Append(progress.Message);
                DetailText.Text = _detailLog.ToString();

                if (progress.Stage == RestartUpdateStage.Completed) {
                    TitleText.Text = "更新完成";
                }
                else if (progress.Stage == RestartUpdateStage.Failed) {
                    TitleText.Text = "更新失败";
                }
            });
        }

        public void ShowError(string message) {
            Dispatcher.Invoke(() => {
                TitleText.Text = "更新失败";
                StatusText.Text = message;
                UpdateProgressBar.Foreground = System.Windows.Media.Brushes.Red;
            });
        }
    }
}
