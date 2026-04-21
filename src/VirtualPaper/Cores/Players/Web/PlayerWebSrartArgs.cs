using System.IO;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Cores.Players.Web {
    record PlayerWebSrartArgs(IWpPlayerData Data, bool IsPreview, Dictionary<string, object>? extraFields = null) {
        public string ToJson() {
            var args = new StartArgsWeb() {
                IsPreview = this.IsPreview,

                FilePath = Data.FilePath,
                WpBasicDataFilePath = Path.Combine(Data.FolderPath, Constants.Field.WpBasicDataFileName),

                DepthFilePath = Data.RType == WpRuntimeType.RImage3D
                    ? Data.DepthFilePath
                    : null,

                WpEffectFilePathUsing = Data.WpEffectFilePathUsing,
                WpEffectFilePathTemporary = Data.WpEffectFilePathTemporary,
                WpEffectFilePathTemplate = Data.WpEffectFilePathTemplate,

                RuntimeType = Data.RType.ToString(),

                SystemBackdrop = App.UserSettings.Settings.SystemBackdrop,
                ApplicationTheme = App.UserSettings.Settings.ApplicationTheme,
                Language = App.UserSettings.Settings.Language,

                Extra = JsonSerializer.Serialize(extraFields),
                IsDebug = false,
            };

            var json = JsonSerializer.Serialize(args, StartArgsWebContext.Default.StartArgsWeb);

            return json;
        }
    }
}
