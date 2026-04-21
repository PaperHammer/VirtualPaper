using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores;

namespace VirtualPaper.RepoPanel.Utils {
    public class WallpaperIndexFile {
        public int Version { get; set; } = 1;
        public List<WallpaperIndexEntry> Items { get; set; } = new();

        public async Task RebuildIndexAsync(IEnumerable<string> storeDirs, CancellationToken token = default) {
            var items = new ConcurrentBag<WallpaperIndexEntry>();

            var folders = storeDirs
                .SelectMany(dir => {
                    var root = new DirectoryInfo(dir);
                    return root.Exists ? root.GetDirectories() : Array.Empty<DirectoryInfo>();
                })
                .ToArray();

            var parallelOptions = new ParallelOptions {
                CancellationToken = token,
                MaxDegreeOfParallelism = Environment.ProcessorCount / 2 > 0 ? Environment.ProcessorCount / 2 : 1
            };

            await Parallel.ForEachAsync(folders, parallelOptions, async (folder, ct) => {
                var jsonPath = Path.Combine(folder.FullName, Constants.Field.WpBasicDataFileName);
                if (!File.Exists(jsonPath)) return;

                var data = await JsonSaver.LoadAsync<WpBasicData>(jsonPath, WpBasicDataContext.Default);

                if (!data.IsAvailable()) return;

                var entry = new WallpaperIndexEntry {
                    Uid = data.WallpaperUid,
                    FolderPath = data.FolderPath,
                    JsonPath = jsonPath,
                    CreateTime = data.CreatedTime,
                    Title = data.Title ?? "",
                    Author = data.Authors,
                    Tags = data.Tags.Split(';', StringSplitOptions.RemoveEmptyEntries),
                };
                items.Add(entry);
            });

            Items = items.OrderByDescending(i => i.CreateTime).ToList();
        }
    }
}
