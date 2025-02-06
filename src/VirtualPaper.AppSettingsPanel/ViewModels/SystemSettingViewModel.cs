using System.Globalization;
using System;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.Common.Utils;

namespace VirtualPaper.AppSettingsPanel.ViewModels {
    public partial class SystemSettingViewModel : ObservableObject {
        public string Text_Developer { get; set; } = string.Empty;
        public string Developer_Debug { get; set; } = string.Empty;
        public string Developer_DebugExplain { get; set; } = string.Empty;
        public string Debug { get; set; } = string.Empty;
        public string Developer_Log { get; set; } = string.Empty;
        public string Developer_LogExplain { get; set; } = string.Empty;
        public string Log { get; set; } = string.Empty;

        public SystemSettingViewModel(
            ICommandsClient commandsClient) {
            _commandClient = commandsClient;

            InitText();
        }

        private void InitText() {
            Text_Developer = LanguageUtil.GetI18n(Constants.I18n.Settings_System_Text_Developer);
            Developer_Debug = LanguageUtil.GetI18n(Constants.I18n.Settings_System_Developer_Debug);
            Developer_DebugExplain = LanguageUtil.GetI18n(Constants.I18n.Settings_System_Developer_DebugExplain);
            Debug = LanguageUtil.GetI18n(Constants.I18n.Settings_System_Text_Debug);
            Developer_Log = LanguageUtil.GetI18n(Constants.I18n.Settings_System_Developer_Log);
            Developer_LogExplain = LanguageUtil.GetI18n(Constants.I18n.Settings_System_Developer_LogExplain);
            Log = LanguageUtil.GetI18n(Constants.I18n.Settings_System_Log);
        }

        internal void OpenDebugView() {
            _commandClient.ShowDebugView();
        }

        internal async Task ExportLogsAsync() {
            var saveFile = await WindowsStoragePickers.PickSaveFileAsync(
                _appSettingsPanel.GetWindowHandle(),
                "virtualpaper_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture),
                new() {
                    ["Compressed archive"] = [".zip"]
                }
            );

            if (saveFile != null) {
                try {
                    LogUtil.ExportLogFiles(saveFile.Path);
                }
                catch (Exception ex) {
                    _appSettingsPanel.GetNotify().ShowExp(ex);
                }
            }
        }

        internal IAppSettingsPanel _appSettingsPanel;
        private readonly ICommandsClient _commandClient;
    }
}
