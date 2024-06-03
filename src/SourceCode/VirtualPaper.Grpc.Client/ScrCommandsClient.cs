using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.ScreenSaver.Grpc.Service.ScrCommands;
using ProcInfoData = VirtualPaper.ScreenSaver.Grpc.Service.ScrCommands.ProcInfoData;

namespace VirtualPaper.Grpc.Client
{
    public class ScrCommandsClient : IScrCommandsClient
    {
        public ScrCommandsClient()
        {
            _client = new ScrCommandsService.ScrCommandsServiceClient(new NamedPipeChannel(".", Constants.SingleInstance.GrpcPipeServerName));                   
        }

        public async void Start(IMetaData metaData)
        {
            WpMetaData wp = new()
            {
                Type = (ScreenSaver.Grpc.Service.ScrCommands.WallpaperType)metaData.Type,
                FilePath = metaData.FilePath,
            };
            await _client.StartAsync(wp);
        }

        public async void Stop()
        {
            await _client.StopAsync(new Empty());
        }

        public async void ChangeLockStatu(bool isLock)
        {
            await _client.ChangeLockStatuAsync(new LockData()
            {
                IsLock = isLock,
            });
        }

        public void AddToWhiteList(string procName)
        {
            _client.AddToWhiteListAsync(new ProcInfoData()
            {
                ProcName = procName,
            });
        }

        public void RemoveFromWhiteList(string procName)
        {
            _client.RemoveFromWhiteListAsync(new ProcInfoData()
            {
                ProcName = procName,
            });
        }

        private readonly ScrCommandsService.ScrCommandsServiceClient _client;
    }
}
