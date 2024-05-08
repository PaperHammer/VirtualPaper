using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.Commands;

namespace VirtualPaper.Grpc.Client
{
    public class CommandsClient : ICommandsClient
    {
        public CommandsClient()
        {
            _client = new CommandsService.CommandsServiceClient(new NamedPipeChannel(".", Constants.SingleInstance.GrpcPipeServerName));
        }

        public async Task ShowUI()
        {
            await _client.ShowUIAsync(new Empty());
        }

        public async Task CloseUI()
        {
            await _client.CloseUIAsync(new Empty());
        }

        public async Task RestartUI()
        {
            await _client.RestartUIAsync(new Empty());
        }

        public async Task ShowDebugView()
        {
            await _client.ShowDebugViewAsync(new Empty());
        }

        public async Task ShutDown()
        {
            await _client.ShutDownAsync(new Empty());
        }

        public void SaveRectUI()
        {
            _client.SaveRectUI(new Empty());
        }

        public async Task SaveRectUIAsync()
        {
            await _client.SaveRectUIAsync(new Empty());
        }

        private readonly CommandsService.CommandsServiceClient _client;
    }
}
