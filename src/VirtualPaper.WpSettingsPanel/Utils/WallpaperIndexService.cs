using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.WpSettingsPanel.Utils.Interfaces;

namespace VirtualPaper.WpSettingsPanel.Utils {
    public partial class WallpaperIndexService : IWallpaperIndexService {
        public TaskCompletionSource<bool> Initialized { get; } = new();

        public async void Initialize(IEnumerable<string> wallpaperInstallDir) {
            try {
                await RebuildAsync(wallpaperInstallDir);
                Initialized.TrySetResult(true);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WallpaperIndexService>().Error(ex);
                Initialized.TrySetException(ex);
            }
        }

        public async Task RebuildAsync(IEnumerable<string> wallpaperInstallDir) {
            var rebuilt = new WallpaperIndexFile();
            await rebuilt.RebuildIndexAsync(wallpaperInstallDir);
            lock (_lock) {
                _index = rebuilt;
                RebuildUidIndexMapUnsafe();
            }
        }

        public IReadOnlyList<WallpaperIndexEntry> Query(int offset, int limit) {
            lock (_lock) return _index.Items.Skip(offset).Take(limit).ToList();
        }

        public IReadOnlyList<WallpaperIndexEntry> Query(int offset, int limit, FilterContext filter) {
            lock (_lock) return WallpaperLibraryQuery.Apply(_index.Items, filter, OrganizationUnsafe)
                .Skip(Math.Max(0, offset)).Take(Math.Max(0, limit)).ToList();
        }

        public IReadOnlyList<string> GetTags() {
            lock (_lock) return WallpaperOrganization.NormalizeTags(_index.Items
                .SelectMany(entry => entry.Tags.Concat(OrganizationUnsafe(entry.Uid).Tags)));
        }

        public bool TryGetValue(string wallpaperUid, out int idx) {
            lock (_lock) return _uidToIndex.TryGetValue(wallpaperUid, out idx);
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
                var newEntry = WallpaperIndexEntry.FromData(data);

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
        private void RebuildUidIndexMapUnsafe() {
            _uidToIndex.Clear();
            for (int i = 0; i < _index.Items.Count; i++) {
                var uid = _index.Items[i].Uid;
                _uidToIndex[uid] = i;
            }
        }
        #endregion

        private readonly object _lock = new();
        private WallpaperIndexFile _index = new();
        private readonly Dictionary<string, int> _uidToIndex = new(StringComparer.OrdinalIgnoreCase);
    }
}
