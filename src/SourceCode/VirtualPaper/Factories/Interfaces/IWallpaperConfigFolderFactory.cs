using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Factories.Interfaces
{
    public interface IWallpaperConfigFolderFactory
    {
        string CreateWpConfigFolder(IMetaData mateData, IMonitor display, IUserSettingsService userSettings);
    }
}
