using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Factories
{
    public class WallpaperConfigFolderFactory : IWallpaperConfigFolderFactory
    {
        public string CreateWpConfigFolder(IMetaData mateData, IMonitor monitor, IUserSettingsService userSettings)
        {
            WallpaperArrangement arrangement = userSettings.Settings.WallpaperArrangement;

            string customizePathCopy = string.Empty;
            if (mateData.WpCustomizePath != null)
            {
                //customisable wallpaper, wpConfig.json is present.
                var dataFolder = Path.Combine(userSettings.Settings.WallpaperDir, Constants.CommonPartialPaths.WallpaperInstallDir);
                try
                {
                    //extract last digits of the Monitor class DeviceName, eg: \\.\DISPLAY4 -> 4
                    var monitorNumber = monitor.Content;
                    if (monitorNumber != null)
                    {
                        //Create a directory with the wp foldername in SaveData/wpdata/, copy wpConfig.json into this.
                        //Further modifications are done to the copy file.
                        string wpdataFolder = string.Empty;
                        switch (arrangement)
                        {
                            case WallpaperArrangement.Per:
                                wpdataFolder = Path.Combine(dataFolder, new DirectoryInfo(mateData.FolderPath).Name, monitorNumber);
                                break;
                            case WallpaperArrangement.Expand:
                                wpdataFolder = Path.Combine(dataFolder, new DirectoryInfo(mateData.FolderPath).Name, "Expand");
                                break;
                            case WallpaperArrangement.Duplicate:
                                wpdataFolder = Path.Combine(dataFolder, new DirectoryInfo(mateData.FolderPath).Name, "Duplicate");
                                break;
                        }
                        Directory.CreateDirectory(wpdataFolder);
                        //copy the original file if not found..
                        customizePathCopy = Path.Combine(wpdataFolder, "WpCustomize.json");
                        if (!File.Exists(customizePathCopy))
                        {
                            File.Copy(mateData.WpCustomizePath, customizePathCopy, true);
                        }
                    }
                    else
                    {
                        //todo: fallback, use the original file (restore feature disabled.)
                    }
                }
                catch
                {
                    //todo: fallback, use the original file (restore feature disabled.)
                }
            }

            return customizePathCopy;
        }
    }
}
