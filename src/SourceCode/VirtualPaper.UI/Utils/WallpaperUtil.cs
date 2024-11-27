using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores;
using Windows.Storage;

namespace VirtualPaper.UI.Utils {
    public class WallpaperUtil {
        public static async Task<bool> WallpaperDirectoryUpdateAsync(List<string> wallpaperInstallFolders, string destFolderPath) {
            bool allOperationsSuccessful = true;

            try {
                await foreach (var libData in ImportWallpaperByFoldersAsync(wallpaperInstallFolders)) {
                    var md = libData.Data;
                    md.MoveTo(Path.Combine(destFolderPath, md.BasicData.FolderName));
                }
            }
            catch (Exception ex) {
                allOperationsSuccessful = false;
                BasicUIComponentUtil.ShowExp(ex);
            }

            return allOperationsSuccessful;
        }
        /// <summary>
        /// Load wallpapers from the given parent folder(), only top directory is scanned.
        /// </summary>
        /// <param name="folderPaths">Parent folders to search for subdirectories.</param>
        /// <returns>Sorted(based on Title) wallpaper _data.</returns>
        public static async IAsyncEnumerable<WpLibData> ImportWallpaperByFoldersAsync(List<string> folderPaths) {
            int idx = 0;
            foreach (string storeDir in folderPaths) {
                DirectoryInfo root = new(storeDir);
                DirectoryInfo[] folders = root.GetDirectories();

                foreach (DirectoryInfo folder in folders) {
                    string[] files = Directory.GetFiles(folder.FullName);
                    WpLibData libData = new();
                    foreach (string file in files) {
                        if (Path.GetFileName(file) == Constants.Field.WpBasicDataFileName) {
                            libData.Data.BasicData = await JsonStorage<WpBasicData>.LoadDataAsync(file);
                        }
                        else if (Path.GetFileName(file) == Constants.Field.WpRuntimeDataFileName) {
                            libData.Data.RuntimeData = await JsonStorage<WpRuntimeData>.LoadDataAsync(file);
                        }

                        if (libData.Data.BasicData.IsAvailable() && !libData.Data.BasicData.IsSingleRType
                            || libData.Data.RuntimeData.IsAvailable()) {
                            libData.Idx = idx++;
                            yield return libData;
                            break;
                        }
                    }
                }
            }
        }

        public static ImportValue ImportSingleFile(IReadOnlyList<IStorageItem> items) {
            if (items.Count > 1) {
                return new(string.Empty, FileType.FUnknown);
            }
            
            if (items[0] is StorageFile file) {
                return new(file.Path, FileFilter.GetFileType(file.Path));
            }

            return new(string.Empty, FileType.FUnknown);
        }

        public static async Task<List<ImportValue>> ImportMultipleFileAsync(IReadOnlyList<IStorageItem> items) {
            ConcurrentBag<ImportValue> importRes = [];
            SemaphoreSlim semaphore = new(20); // 并发度控制

            var tasks = items.Select(async item => {
                await semaphore.WaitAsync();

                try {
                    if (item is StorageFile file) {
                        importRes.Add(new(file.Path, FileFilter.GetFileType(file.Path)));
                    }
                    else if (item is StorageFolder folder) {
                        var subItems = await folder.GetItemsAsync();
                        var subResults = await ImportMultipleFileAsync(subItems);
                        foreach (var res in subResults) {
                            importRes.Add(res);
                        }
                    }
                }
                finally {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return [.. importRes];
        }
    }

    public struct ImportValue {
        public string FilePath { get; set; } = string.Empty;
        public FileType FType { get; set; } = FileType.FUnknown;

        public ImportValue() { }

        public ImportValue(string filePath, FileType ftype)
        {
            FilePath = filePath;
            FType = ftype;
        }
    }
}
