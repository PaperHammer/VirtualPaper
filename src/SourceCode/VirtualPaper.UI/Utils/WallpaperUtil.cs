using System.Collections.Generic;
using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Models.WallpaperMetaData;
using Windows.Storage;
using WinUI3Localizer;

namespace VirtualPaper.UI.Utils
{
    public class WallpaperUtil
    {
        public static (bool, string) TrytoDropFile(IReadOnlyList<IStorageItem> items)
        {
            if (items.Count > 1)
            {
                return (false, _localizer.GetLocalizedString("Dialog_Content_TryDropFileErr"));
            }

            string filePath = string.Empty;
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    WallpaperType fileType = GetFileTypeFromPath(file.Path);
                    if (fileType == WallpaperType.unknown)
                    {
                        return (false, _localizer.GetLocalizedString("Dialog_Content_TryDropFileErr"));
                    }
                    else
                    {
                        filePath = file.Path;
                        break;
                    }
                }
                else
                {
                    return (false, _localizer.GetLocalizedString("Dialog_Content_TryDropFileErr"));
                }
            }

            return (true, filePath);
        }

        public static string InitUid(IMetaData metaData)
        {
            DirectoryInfo info = new(metaData.FolderPath);
            string folderName = info.Name.Replace(".", "");
            if (metaData.VirtualPaperUid == null || metaData.VirtualPaperUid.Length == 0)
                metaData.VirtualPaperUid = "LCL" + folderName;

            return folderName;
        }

        private static WallpaperType GetFileTypeFromPath(string filePath)
        {
            string extension = Path.GetExtension(filePath)?.ToLowerInvariant();

            return extension switch
            {
                //".html" or ".htm" => WallpaperType.web,
                ".apng" or ".gif" => WallpaperType.gif,
                ".jpg" or ".jpeg" or ".png" or ".bmp" or ".svg" or ".webp" => WallpaperType.picture,
                ".mp4" or ".webm" => WallpaperType.video,
                _ => WallpaperType.unknown,
            };
        }

        private static ILocalizer _localizer = Localizer.Get();
    }
}
