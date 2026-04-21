using VirtualPaper.Common;
using VirtualPaper.Grpc.Service.CommonModels;

namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface IDeskPetControlClient {
        Task<Grpc_DpBasicData?> CreateBasicDataAsync(string filePath, DeskPetEngineType type, CancellationToken token);
    }
}
