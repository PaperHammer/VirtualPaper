using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Factories.Interfaces {
    public interface IWallpaperConfigFolderFactory {
        string CreateWpConfigFolder(
            IWpMetadata data,
            string monitorContent,
            IUserSettingsService userSettings);
    }
}
