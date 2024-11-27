using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Grpc.Service.ScrCommands;

namespace VirtualPaper.Grpc.Client {
    public class ScrCommandsClient : IScrCommandsClient {
        public ScrCommandsClient() {
            _client = new Grpc_ScrCommandsService.Grpc_ScrCommandsServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));
        }

        public async void Start(Grpc_WpBasicData grpc_data) {
            await _client.StartAsync(grpc_data);
        }

        public async void Stop() {
            await _client.StopAsync(new Empty());
        }

        public async void ChangeLockStatu(bool isLock) {
            await _client.ChangeLockStatuAsync(new Grpc_LockData() {
                IsLock = isLock,
            });
        }

        public void AddToWhiteList(string procName) {
            _client.AddToWhiteListAsync(new Grpc_ProcInfoData() {
                ProcName = procName,
            });
        }

        public void RemoveFromWhiteList(string procName) {
            _client.RemoveFromWhiteListAsync(new Grpc_ProcInfoData() {
                ProcName = procName,
            });
        }

        private readonly Grpc_ScrCommandsService.Grpc_ScrCommandsServiceClient _client;
    }
}
