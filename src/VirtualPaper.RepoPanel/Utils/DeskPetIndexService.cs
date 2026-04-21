using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Models.RepoPanel.Interfaces;

namespace VirtualPaper.RepoPanel.Utils {
    public class DeskPetIndexService {
        public TaskCompletionSource<bool> Initialized { get; } = new();

        public async void Initialize(IEnumerable<string> wallpaperInstallDir) {
            await _index.RebuildIndexAsync(wallpaperInstallDir);
            Initialized.TrySetResult(true);
        }

        public IReadOnlyList<DeskPetIndexEntry> Query(int offset, int limit) {
            return _index.Items.Skip(offset).Take(limit).ToList();
        }

        public bool TryGetValue(string wallpaperUid, out int idx) {
            return _uidToIndex.TryGetValue(wallpaperUid, out idx);
        }

        public void Remove(IDpBasicData data) {
            if (data == null || !data.IsAvailable())
                return;

            lock (_lock) {
                if (!_uidToIndex.TryGetValue(data.Uid, out var idx))
                    return;

                if (idx < 0 || idx >= _index.Items.Count)
                    return;

                _index.Items.RemoveAt(idx);
                _uidToIndex.Remove(data.Uid);
                RebuildUidIndexMapUnsafe();
            }
        }

        public void Update(IDpBasicData data) {
            if (data == null || !data.IsAvailable())
                return;

            lock (_lock) {
                var newEntry = BuildEntryFromData(data);

                if (_uidToIndex.TryGetValue(data.Uid, out var idx) &&
                    idx >= 0 && idx < _index.Items.Count) {
                    _index.Items[idx] = newEntry;
                }
                else {
                    _index.Items.Add(newEntry);
                    idx = _index.Items.Count - 1;
                    RebuildUidIndexMapUnsafe();
                }

                _uidToIndex[data.Uid] = idx;
            }
        }

        #region utils
        private static DeskPetIndexEntry BuildEntryFromData(IDpBasicData data) {
            var jsonPath = Path.Combine(data.FolderPath, Constants.Field.DpBasicDataFileName);
            var entry = new DeskPetIndexEntry {
                Uid = data.Uid,
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
        private readonly DeskPetIndexFile _index = new();
        private readonly Dictionary<string, int> _uidToIndex = new(StringComparer.OrdinalIgnoreCase);
    }
}
