using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.WpSettingsPanel.Utils.Interfaces {
    public interface IWallpaperIndexService {
        TaskCompletionSource<bool> Initialized { get; }

        void Initialize(IEnumerable<string> wallpaperInstallDir);

        Task RebuildAsync(IEnumerable<string> wallpaperInstallDir);

        IReadOnlyList<WallpaperIndexEntry> Query(int offset, int limit);
        IReadOnlyList<WallpaperIndexEntry> Query(int offset, int limit, FilterContext filter);
        IReadOnlyList<string> GetTags();
        WallpaperOrganization GetOrganization(string uid);
        Task UpdateOrganizationAsync(IEnumerable<string> uids, bool? favorite = null,
            IEnumerable<string>? tags = null, WallpaperTagUpdate tagUpdate = WallpaperTagUpdate.Replace);

        bool TryGetValue(string wallpaperUid, out int idx);

        void Remove(IWpBasicData data);

        void Update(IWpBasicData data);
    }
}
