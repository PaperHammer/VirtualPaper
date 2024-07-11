using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.Utils;
using Windows.Storage.Pickers;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels.AppSettings
{
    public class SystemSettingViewModel : ObservableObject
    {
        public string Text_Developer { get; set; } = string.Empty;
        public string Developer_Debug { get; set; } = string.Empty;
        public string Developer_DebugExplain { get; set; } = string.Empty;
        public string Debug { get; set; } = string.Empty;
        public string Developer_Log { get; set; } = string.Empty;
        public string Developer_LogExplain { get; set; } = string.Empty;
        public string Log { get; set; } = string.Empty;

        public SystemSettingViewModel(
            ICommandsClient commandsClient,
            IDialogService dialogService)
        {
            _commandClient = commandsClient;
            _dialogService = dialogService;

            InitText();
        }

        private void InitText()
        {
            _localizer = Localizer.Get();

            Text_Developer = _localizer.GetLocalizedString("Settings_System_Text_Developer");
            Developer_Debug = _localizer.GetLocalizedString("Settings_System_Developer_Debug");
            Developer_DebugExplain = _localizer.GetLocalizedString("Settings_System_Developer_DebugExplain");
            Debug = _localizer.GetLocalizedString("Settings_System_Text_Debug");
            Developer_Log = _localizer.GetLocalizedString("Settings_System_Developer_Log");
            Developer_LogExplain = _localizer.GetLocalizedString("Settings_System_Developer_LogExplain");
            Log = _localizer.GetLocalizedString("Settings_System_Log");
        }

        internal void OpenDebugView()
        {
            _commandClient.ShowDebugView();
        }

        internal async Task ExportLogsAsync()
        {
            var filePicker = new FileSavePicker();
            filePicker.SetOwnerWindow(App.Services.GetRequiredService<MainWindow>());
            filePicker.FileTypeChoices.Add("Compressed archive", [".zip"]);
            filePicker.SuggestedFileName = "virtualpaper_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var file = await filePicker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    LogUtil.ExportLogFiles(file.Path);
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowDialogAsync(
                        ex.Message
                        , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                        , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
                    return;
                }
            }
        }

        private ILocalizer _localizer;
        private ICommandsClient _commandClient;
        private IDialogService _dialogService;
    }
}
