using VirtualPaper.Common;

namespace VirtualPaper.Cores.PlaybackControl {
    public interface IPlayback : IDisposable {
        event EventHandler<PlaybackMode>? PlaybackModeChanged;
        PlaybackMode WallpaperPlaybackMode { get; set; }
        void Start();
        void Stop();
    }
}
