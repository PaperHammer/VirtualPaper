using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;
using Windows.Storage;

namespace VirtualPaper.AppSettingsPanel.ViewModels {
    public partial class SystemSettingViewModel {
        public ICommand? DebugCommand { get; set; }
        public ICommand? LogCommand { get; set; }

        public SystemSettingViewModel(
            ICommandsClient commandsClient,
            IStoragePicker storagePicker) {
            _commandClient = commandsClient;
            _storagePicker = storagePicker;

            InitCommand();
        }

        private void InitCommand() {
            DebugCommand = new RelayCommand(OpenDebugView);
            LogCommand = new RelayCommand(async () => {
                await ExportLogsAsync();
            });
        }

        private void OpenDebugView() {
            _commandClient.ShowDebugView();
        }

        public async Task ExportLogsAsync() {
            var saveFile = await InternalExportLogsAsync();

            if (saveFile != null) {
                try {
                    LogUtil.ExportLogFiles(saveFile.Path);
                }
                catch (Exception ex) {
                    ArcLog.GetLogger<SystemSettingViewModel>().Error(ex);
                    GlobalMessageUtil.ShowException(ex);
                }
            }
        }

        public async Task<IStorageFile?> InternalExportLogsAsync() {
            return await _storagePicker.PickSaveFileAsync(
                WindowConsts.WindowHandle,
                "virtualpaper_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture),
                new System.Collections.Generic.Dictionary<string, string[]>() {
                    ["Compressed archive"] = [".zip"]
                }
            );
        }

        private readonly ICommandsClient _commandClient;
        private readonly IStoragePicker _storagePicker;
    }
}
