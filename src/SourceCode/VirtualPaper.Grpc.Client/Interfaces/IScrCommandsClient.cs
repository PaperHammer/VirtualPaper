using VirtualPaper.Models.WallpaperMetaData;

namespace VirtualPaper.Grpc.Client.Interfaces
{
    public interface IScrCommandsClient
    {
        void AddToWhiteList(string procName);
        void ChangeLockStatu(bool isLock);
        void RemoveFromWhiteList(string procName);
        void Start(IMetaData metaData);
        void Stop();
    }
}
