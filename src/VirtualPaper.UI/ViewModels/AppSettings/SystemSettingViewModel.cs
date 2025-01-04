using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.Utils;
using Windows.Storage.Pickers;

namespace VirtualPaper.UI.ViewModels.AppSettings {
    public partial class SystemSettingViewModel : ObservableObject {
        public string Text_Developer { get; set; } = string.Empty;
        public string Developer_Debug { get; set; } = string.Empty;
        public string Developer_DebugExplain { get; set; } = string.Empty;
        public string Debug { get; set; } = string.Empty;
        public string Developer_Log { get; set; } = string.Empty;
        public string Developer_LogExplain { get; set; } = string.Empty;
        public string Log { get; set; } = string.Empty;

        public SystemSettingViewModel(
            ICommandsClient commandsClient,
            IDialogService dialogService) {
            _commandClient = commandsClient;
            _dialogService = dialogService;
            
            InitText();
        }

        private void InitText() {           
            Text_Developer = App.GetI18n(Constants.I18n.Settings_System_Text_Developer);
            Developer_Debug = App.GetI18n(Constants.I18n.Settings_System_Developer_Debug);
            Developer_DebugExplain = App.GetI18n(Constants.I18n.Settings_System_Developer_DebugExplain);
            Debug = App.GetI18n(Constants.I18n.Settings_System_Text_Debug);
            Developer_Log = App.GetI18n(Constants.I18n.Settings_System_Developer_Log);
            Developer_LogExplain = App.GetI18n(Constants.I18n.Settings_System_Developer_LogExplain);
            Log = App.GetI18n(Constants.I18n.Settings_System_Log);
        }

        internal void OpenDebugView() {
            _commandClient.ShowDebugView();
        }

        internal async Task ExportLogsAsync() {
            var filePicker = new FileSavePicker();
            filePicker.SetOwnerWindow(App.Services.GetRequiredService<MainWindow>());
            filePicker.FileTypeChoices.Add("Compressed archive", [".zip"]);
            filePicker.SuggestedFileName = "virtualpaper_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var file = await filePicker.PickSaveFileAsync();
            if (file != null) {
                try {
                    LogUtil.ExportLogFiles(file.Path);
                }
                catch (Exception ex) {
                    await _dialogService.ShowDialogAsync(
                        ex.Message
                        , App.GetI18n(Constants.I18n.Dialog_Title_Prompt)
                        , App.GetI18n(Constants.I18n.Text_Confirm));
                    return;
                }
            }
        }

        private readonly ICommandsClient _commandClient;
        private readonly IDialogService _dialogService;
    }
}
