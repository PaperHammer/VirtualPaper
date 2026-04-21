using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.RepoPanel.Utils {
    public sealed class WallpaperIndexService {
        public TaskCompletionSource<bool> Initialized { get; } = new();

        public async void Initialize(IEnumerable<string> wallpaperInstallDir) {
            await _index.RebuildIndexAsync(wallpaperInstallDir);
            Initialized.TrySetResult(true);
        }

        public IReadOnlyList<WallpaperIndexEntry> Query(int offset, int limit) {
            return _index.Items.Skip(offset).Take(limit).ToList();
        }

        public bool TryGetValue(string wallpaperUid, out int idx) {
            return _uidToIndex.TryGetValue(wallpaperUid, out idx);
        }

        public void Remove(IWpBasicData data) {
            if (data == null || !data.IsAvailable())
                return;

            lock (_lock) {
                if (!_uidToIndex.TryGetValue(data.WallpaperUid, out var idx))
                    return;

                if (idx < 0 || idx >= _index.Items.Count)
                    return;

                _index.Items.RemoveAt(idx);
                _uidToIndex.Remove(data.WallpaperUid);
                RebuildUidIndexMapUnsafe();
            }
        }

        public void Update(IWpBasicData data) {
            if (data == null || !data.IsAvailable())
                return;

            lock (_lock) {
                var newEntry = BuildEntryFromData(data);

                if (_uidToIndex.TryGetValue(data.WallpaperUid, out var idx) &&
                    idx >= 0 && idx < _index.Items.Count) {
                    _index.Items[idx] = newEntry;
                }
                else {
                    _index.Items.Add(newEntry);
                    idx = _index.Items.Count - 1;
                    RebuildUidIndexMapUnsafe();
                }

                _uidToIndex[data.WallpaperUid] = idx;
            }
        }

        #region utils
        private static WallpaperIndexEntry BuildEntryFromData(IWpBasicData data) {
            var jsonPath = Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName);
            var entry = new WallpaperIndexEntry {
                Uid = data.WallpaperUid,
                JsonPath = jsonPath,
                CreateTime = data.CreatedTime,
                Title = data.Title ?? "",
                Author = data.Authors,
                Tags = data.Tags.Split(';', StringSplitOptions.RemoveEmptyEntries),
            };
            return entry;
        }

        private void RebuildUidIndexMapUnsafe() {
            _uidToIndex.Clear();
            for (int i = 0; i < _index.Items.Count; i++) {
                var uid = _index.Items[i].Uid;
                _uidToIndex[uid] = i;
            }
        }
        #endregion

        private readonly object _lock = new();
        private readonly WallpaperIndexFile _index = new();
        private readonly Dictionary<string, int> _uidToIndex = new(StringComparer.OrdinalIgnoreCase);
    }
}
