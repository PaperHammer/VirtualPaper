using VirtualPaper.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Factories.Interfaces {
    public interface IWallpaperFactory {
        IWallpaperPlaying CreatePlayer(
            IWpPlayerData data,
            IMonitor monitor,
            IUserSettingsService userSettings,
            bool isPreview = false);
    }
}
