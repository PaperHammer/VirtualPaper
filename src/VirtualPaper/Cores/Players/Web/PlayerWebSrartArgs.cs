using System.IO;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Cores.Players.Web {
    record PlayerWebSrartArgs(
        IWpPlayerData Data,
        bool IsPreview,
        string? WpBasicDataFilePath = null,
        Dictionary<string, object>? ExtraFields = null,
        ISettings? Settings = null) {
        public string ToJson() {
            var settings = Settings ?? App.UserSettings.Settings;
            var args = new StartArgsWeb() {
                IsPreview = this.IsPreview,

                FilePath = Data.FilePath,
                WpBasicDataFilePath = string.IsNullOrWhiteSpace(WpBasicDataFilePath)
                    ? Path.Combine(Data.FolderPath, Constants.Field.WpBasicDataFileName)
                    : WpBasicDataFilePath,

                DepthFilePath = Data.RType == RuntimeType.RImage3D
                    ? Data.DepthFilePath
                    : null,

                WpEffectFilePathUsing = Data.WpEffectFilePathUsing,
                WpEffectFilePathTemporary = Data.WpEffectFilePathTemporary,
                WpEffectFilePathTemplate = Data.WpEffectFilePathTemplate,

                RuntimeType = Data.RType.ToString(),

                SystemBackdrop = settings.SystemBackdrop,
                ApplicationTheme = settings.ApplicationTheme,
                Language = settings.Language,

                Extra = JsonSerializer.Serialize(ExtraFields),
                IsDebug = false,
            };

            var json = JsonSerializer.Serialize(args, StartArgsWebContext.Default.StartArgsWeb);

            return json;
        }
    }
}
