using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Models.WallpaperMetaData;
using Windows.Storage;
using WinUI3Localizer;

namespace VirtualPaper.UI.Utils
{
    public class WallpaperUtil
    {
        public static string ImportSingleFile(IReadOnlyList<IStorageItem> items)
        {
            if (items.Count > 1)
            {
                return null;
            }

            string filePath = null;
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    WallpaperType fileType = GetFileTypeFromPath(file.Path);
                    if (fileType != WallpaperType.unknown)
                    {
                        filePath = file.Path;
                        break;
                    }
                }
            }

            return filePath;
        }

        public static async Task<List<string>> ImportMultipleFileAsync(IReadOnlyList<IStorageItem> items)
        {
            ConcurrentBag<string> filePaths = [];
            SemaphoreSlim semaphore = new(20); // 并发度控制

            var tasks = items.Select(async item =>
            {
                await semaphore.WaitAsync();

                try
                {
                    if (item is StorageFile file)
                    {
                        filePaths.Add(file.Path);
                    }
                    else if (item is StorageFolder folder)
                    {
                        var files = await folder.GetFilesAsync();
                        foreach (var subFile in files)
                        {
                            filePaths.Add(subFile.Path);
                        }
                    }
                }
                finally
                {
                    semaphore.Release(); 
                }
            });

            await Task.WhenAll(tasks);

            return [.. filePaths];
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
