using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NLog;
using VirtualPaper.Cores.ScreenSaver;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.ScreenSaver.Grpc.Service.ScrCommands;

namespace VirtualPaper.GrpcServers
{
    public class ScrCommandsServer : ScrCommandsService.ScrCommandsServiceBase
    {
        public ScrCommandsServer(
            IScrControl scrControl)
        {
            _scrControl = scrControl;
        }

        public override Task<Empty> ChangeLockStatu(LockData request, ServerCallContext context)
        {
            try
            {
                _scrControl.ChangeLockStatu(request.IsLock);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> Start(WpMetaData request, ServerCallContext context)
        {
            try
            {
                MetaData metaData = new()
                {
                    FilePath = request.FilePath,
                    Type = (Common.WallpaperType)request.Type,
                };
                _scrControl.Start(metaData);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> Stop(Empty request, ServerCallContext context)
        {
            try
            {
                _scrControl.Stop();
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> AddToWhiteList(ProcInfoData request, ServerCallContext context)
        {
            try
            {
                _scrControl.AddToWhiteList(request.ProcName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> RemoveFromWhiteList(ProcInfoData request, ServerCallContext context)
        {
            try
            {
                _scrControl.RemoveFromWhiteList(request.ProcName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

            return Task.FromResult(new Empty());
        }

        private IScrControl _scrControl;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
