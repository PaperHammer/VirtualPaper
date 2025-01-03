﻿using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using VirtualPaper.Cores.ScreenSaver;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Grpc.Service.ScrCommands;

namespace VirtualPaper.GrpcServers {
    public class ScrCommandsServer(
        IScrControl scrControl) : Grpc_ScrCommandsService.Grpc_ScrCommandsServiceBase {
        public override Task<Empty> ChangeLockStatu(Grpc_LockData request, ServerCallContext context) {
            _scrControl.ChangeLockStatu(request.IsLock);

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> Start(Empty request, ServerCallContext context) {
            _scrControl.Start();

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> Stop(Empty request, ServerCallContext context) {
            _scrControl.Stop();

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> AddToWhiteList(Grpc_ProcInfoData request, ServerCallContext context) {
            _scrControl.AddToWhiteList(request.ProcName);

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> RemoveFromWhiteList(Grpc_ProcInfoData request, ServerCallContext context) {
            _scrControl.RemoveFromWhiteList(request.ProcName);

            return Task.FromResult(new Empty());
        }

        private readonly IScrControl _scrControl = scrControl;
    }
}
