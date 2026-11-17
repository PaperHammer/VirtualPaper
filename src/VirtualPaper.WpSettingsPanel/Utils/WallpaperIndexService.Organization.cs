using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.WpSettingsPanel.Utils {
    public partial class WallpaperIndexService {
        public WallpaperIndexService() : this(Path.Combine(Constants.CommonPaths.AppDataDir, "wallpaper_library.json")) { }

        internal WallpaperIndexService(string organizationPath) {
            _organizationPath = organizationPath;
            try {
                if (File.Exists(organizationPath)) {
                    var state = JsonSerializer.Deserialize(File.ReadAllText(organizationPath),
                        WallpaperLibraryStateContext.Default.WallpaperLibraryState)
                        ?? throw new InvalidDataException("Wallpaper library preferences are empty.");
                    _organization = new(state.Items ?? throw new InvalidDataException("Wallpaper library preferences have no items."),
                        StringComparer.OrdinalIgnoreCase);
                    foreach (var uid in _organization.Keys.ToArray()) {
                        var item = _organization[uid] ?? throw new InvalidDataException("Invalid wallpaper library preference.");
                        _organization[uid] = item with { Tags = WallpaperOrganization.NormalizeTags(item.Tags ?? []) };
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) {
                _organizationLoadError = ex;
                _organization.Clear();
                ArcLog.GetLogger<WallpaperIndexService>().Error(ex);
            }
        }

        public WallpaperOrganization GetOrganization(string uid) {
            lock (_lock) {
                var item = OrganizationUnsafe(uid);
                return item with { Tags = item.Tags.ToArray() };
            }
        }

        public async Task UpdateOrganizationAsync(IEnumerable<string> uids, bool? favorite = null,
            IEnumerable<string>? tags = null, WallpaperTagUpdate tagUpdate = WallpaperTagUpdate.Replace) {
            var selected = uids.Where(uid => !string.IsNullOrWhiteSpace(uid)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (selected.Length == 0 || (favorite == null && tags == null)) return;
            var normalized = tags == null ? null : WallpaperOrganization.NormalizeTags(tags);
            await _organizationGate.WaitAsync();
            try {
                // Never silently replace an unreadable preferences file with an empty state.
                if (_organizationLoadError != null) throw new IOException("Wallpaper library preferences could not be loaded.", _organizationLoadError);
                Dictionary<string, WallpaperOrganization> updated;
                lock (_lock) updated = new(_organization, StringComparer.OrdinalIgnoreCase);
                foreach (string uid in selected) {
                    var previous = updated.GetValueOrDefault(uid) ?? new WallpaperOrganization();
                    var nextTags = normalized == null ? previous.Tags : tagUpdate switch {
                        WallpaperTagUpdate.Add => WallpaperOrganization.NormalizeTags(previous.Tags.Concat(normalized)),
                        WallpaperTagUpdate.Remove => previous.Tags.Except(normalized, StringComparer.OrdinalIgnoreCase).ToArray(),
                        _ => normalized
                    };
                    var next = previous with { Favorite = favorite ?? previous.Favorite, Tags = nextTags };
                    if (!next.Favorite && next.Tags.Length == 0) updated.Remove(uid);
                    else updated[uid] = next;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_organizationPath))!);
                await FileUtil.WriteAllTextAsync(_organizationPath, JsonSerializer.Serialize(
                    new WallpaperLibraryState { Items = updated }, WallpaperLibraryStateContext.Default.WallpaperLibraryState), new UTF8Encoding(false));
                lock (_lock) {
                    _organization = updated;
                    InvalidateCachesUnsafe(tagsChanged: normalized != null);
                }
            }
            finally { _organizationGate.Release(); }
        }

        private WallpaperOrganization OrganizationUnsafe(string uid) => _organization.GetValueOrDefault(uid) ?? EmptyOrganization;
        private static readonly WallpaperOrganization EmptyOrganization = new();
        private readonly string _organizationPath;
        private readonly Exception? _organizationLoadError;
        private readonly SemaphoreSlim _organizationGate = new(1, 1);
        private Dictionary<string, WallpaperOrganization> _organization = new(StringComparer.OrdinalIgnoreCase);
    }
}
