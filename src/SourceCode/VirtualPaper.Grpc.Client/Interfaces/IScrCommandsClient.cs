using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface IScrCommandsClient {
        void AddToWhiteList(string procName);
        void ChangeLockStatu(bool isLock);
        void RemoveFromWhiteList(string procName);
        void Start(Grpc_WpBasicData grpc_data);
        void Stop();
    }
}
