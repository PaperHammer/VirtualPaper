using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Services {
    /// <summary>
    /// Completes defaults that depend on other settings. Fixed defaults belong
    /// in the Settings model. Returns whether the completed settings need saving.
    /// </summary>
    internal static class SettingsDefaults {
        internal static bool Apply(ISettings settings) {
            bool changed = false;

            if (string.IsNullOrWhiteSpace(settings.BuiltInWallpapersDir)) {
                settings.BuiltInWallpapersDir = Constants.CommonPaths.GetBuiltInWallpapersDir(settings.WallpaperDir);
                changed = true;
            }

            return changed;
        }
    }
}
