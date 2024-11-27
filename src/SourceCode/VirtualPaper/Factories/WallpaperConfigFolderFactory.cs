using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Factories {
    public class WallpaperConfigFolderFactory : IWallpaperConfigFolderFactory {
        public string CreateWpConfigFolder(
            IWpMetadata data,
            string monitorContent,
            IUserSettingsService userSettings) {
            WallpaperArrangement arrangement = userSettings.Settings.WallpaperArrangement;

            string wpEffectFilePathUsing = string.Empty;
            if (data.RuntimeData.WpEffectFilePathTemplate != null) {
                //customisable wallpaper, wpConfig.json is present.
                var dataFolder = Path.Combine(userSettings.Settings.WallpaperDir);
                try {
                    //extract last digits of the Monitor class DeviceName, eg: \\.\DISPLAY4 -> 4
                    //var monitorNumber = monitor.Content;
                    if (monitorContent != null) {
                        //Create a directory with the wp foldername in SaveData/wpdata/, copy wpConfig.json into this.
                        //Further modifications are done to the copy file.
                        string wpdataFolder = string.Empty;
                        switch (arrangement) {
                            case WallpaperArrangement.Per:
                                wpdataFolder = Path.Combine(dataFolder, new DirectoryInfo(data.BasicData.FolderPath).Name, monitorContent);
                                break;
                            case WallpaperArrangement.Expand:
                                wpdataFolder = Path.Combine(dataFolder, new DirectoryInfo(data.BasicData.FolderPath).Name, "Expand");
                                break;
                            case WallpaperArrangement.Duplicate:
                                wpdataFolder = Path.Combine(dataFolder, new DirectoryInfo(data.BasicData.FolderPath).Name, "Duplicate");
                                break;
                        }
                        Directory.CreateDirectory(wpdataFolder);
                        //copy the original file if not found..
                        wpEffectFilePathUsing = Path.Combine(wpdataFolder, Constants.Field.WpEffectFilePathUsing);
                        if (!File.Exists(wpEffectFilePathUsing)) {
                            File.Copy(data.RuntimeData.WpEffectFilePathTemplate, wpEffectFilePathUsing, true);
                        }
                    }
                    else {
                        //todo: fallback, use the original file (restore feature disabled.)
                    }
                }
                catch {
                    //todo: fallback, use the original file (restore feature disabled.)
                }
            }

            return wpEffectFilePathUsing;
        }
    }
}
