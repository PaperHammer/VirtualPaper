using VirtualPaper.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Factories.Interfaces
{
    public interface IWallpaperFactory
    {
        IWallpaper CreateWallpaper(IMetaData mateData, IMonitor monitor, IUserSettingsService userSettings, bool isPreview = false);
    }
}
