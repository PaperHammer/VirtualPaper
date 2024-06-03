using VirtualPaper.Models.WallpaperMetaData;

namespace VirtualPaper.Cores.ScreenSaver
{
    public interface IScrControl : IDisposable
    {
        bool IsRunning { get; }
        void AddToWhiteList(string procName);
        void ChangeLockStatu(bool isLock);
        void RemoveFromWhiteList(string procName);
        void Start(IMetaData metaData);
        void Stop();
    }
}
