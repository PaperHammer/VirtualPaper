using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;

namespace VirtualPaper.Grpc.Client {
    public class DeskPetControlClient : IDeskPetControlClient {
        public Task<Grpc_DpBasicData?> CreateBasicDataAsync(string filePath, DeskPetEngineType type, CancellationToken token) {
            throw new NotImplementedException();
        }
    }
}
