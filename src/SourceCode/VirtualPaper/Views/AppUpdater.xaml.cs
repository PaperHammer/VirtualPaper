using Microsoft.Extensions.DependencyInjection;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using VirtualPaper.Common;
using VirtualPaper.Services.Interfaces;
using DownloadProgressEventArgs = VirtualPaper.Services.Interfaces.DownloadProgressEventArgs;
using IDownloadService = VirtualPaper.Services.Interfaces.IDownloadService;
using MessageBox = System.Windows.MessageBox;

namespace VirtualPaper.Views
{
    /// <summary>
    /// AppUpdater.xaml 的交互逻辑
    /// </summary>
    public partial class AppUpdater : Window
    {
        public AppUpdater(Uri fileUri, string changelogText)
        {
            InitializeComponent();

            if (fileUri != null)
            {
                BtnDownload.Content = App.GetResourceDicString("AppUpdater_Update_Text_BtnDownload");
                BtnInstall.Content = App.GetResourceDicString("AppUpdater_Update_Text_BtnInstall");

                _suggestedFileName = fileUri.Segments.Last();
                _fileUrl = fileUri;
                Changelog.Markdown = changelogText;
            }
            else
            {
                this.Close();
            }
        }

        private void Download_DownloadStarted(object? sender, DownloadEventArgs e)
        {
            _ = this.Dispatcher.BeginInvoke(new Action(() =>
            {
                DownloadProgressText.Text = $"0.00 MB / {e.TotalSize:0.00} MB";
            }));
        }

        private void UpdateDownload_DownloadProgressChanged(object? sender, DownloadProgressEventArgs e)
        {
            _ = this.Dispatcher.BeginInvoke(new Action(() =>
            {
                ProgressBar.Value = e.Percentage;
                TaskbarItemInfo.ProgressValue = e.Percentage / 100f;
                DownloadProgressText.Text = $"{e.DownloadedSize:0.00} MB / {e.TotalSize:0.00} MB";
            }));
        }

        private void UpdateDownload_DownloadFileCompleted(object? sender, DownloadCompletedEventArgs e)
        {
            _ = this.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.IsCompleted && e.IsNormal)
                {
                    ResetBtnVisibility();
                    BtnInstall.Visibility = Visibility.Visible;

                    _isDownloadComplete = true;
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                }
                else
                {
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;
                    Changelog.Markdown = App.GetResourceDicString("AppUpdater_Update_ExceptionAppUpdateFail");
                    _forceClose = true;
                }
            }));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_forceClose != true && _download != null)
            {
                if (MessageBox.Show(
                    App.GetResourceDicString("AppUpdater_Update_DescriptionCancelQuestion"),
                    App.GetResourceDicString("AppUpdater_Propt"), 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _forceClose = true;
                }
                else
                {
                    e.Cancel = true;
                }
            }
            else
            {
                if (_download != null)
                {
                    _download.DownloadFileCompleted -= UpdateDownload_DownloadFileCompleted;
                    _download.DownloadProgressChanged -= UpdateDownload_DownloadProgressChanged;
                    _download.Cancel();
                }
            }
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            BtnDownload.IsEnabled = false;

            try
            {
                _download = App.Services.GetRequiredService<IDownloadService>();
                _savePath = Path.Combine(Constants.CommonPaths.TempDir, _suggestedFileName);
                _download.DownloadFile(_fileUrl, _savePath);
                _download.DownloadFileCompleted += UpdateDownload_DownloadFileCompleted;
                _download.DownloadProgressChanged += UpdateDownload_DownloadProgressChanged;
                _download.DownloadStarted += Download_DownloadStarted;
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;
                Changelog.Markdown = App.GetResourceDicString("AppUpdater_Update_ExceptionAppUpdateFail");
                _forceClose = true;
                BtnDownload.IsEnabled = true;
            }
        }

        private void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloadComplete)
            {
                try
                {
                    _forceClose = true;
                    //run setup in silent mode.
                    Process.Start(_savePath, "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS");
                    //inno installer will auto retry, waiting for application exit.
                    App.ShutDown();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    MessageBox.Show(
                        App.GetResourceDicString("AppUpdater_Update_ExceptionAppUpdateFail"),
                        App.GetResourceDicString("AppUpdater_TextError"));
                }
            }
        }

        private void ResetBtnVisibility()
        {
            BtnDownload.Visibility = Visibility.Collapsed;
            BtnInstall.Visibility = Visibility.Collapsed;
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private IDownloadService _download;
        private readonly Uri _fileUrl;
        private bool _forceClose = false;
        private bool _isDownloadComplete = false;
        private readonly string _suggestedFileName;
        private string _savePath = string.Empty;
    }
}
