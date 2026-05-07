using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.WpSettingsPanel.Utils.Interfaces {
    public interface IWallpaperIndexService {
        TaskCompletionSource<bool> Initialized { get; }

        void Initialize(IEnumerable<string> wallpaperInstallDir);

        IReadOnlyList<WallpaperIndexEntry> Query(int offset, int limit);

        bool TryGetValue(string wallpaperUid, out int idx);

        void Remove(IWpBasicData data);

        void Update(IWpBasicData data);
    }
}
