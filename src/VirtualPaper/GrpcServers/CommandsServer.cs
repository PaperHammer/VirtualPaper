using System.IO;
using System.Windows.Threading;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Grpc.Service.Commands;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Views;
using Application = System.Windows.Application;

namespace VirtualPaper.GrpcServers {
    public class CommandsServer(
        IUIRunnerService runner) : Grpc_CommandsService.Grpc_CommandsServiceBase {
        public override async Task<Empty> ShowUI(Empty _, ServerCallContext context) {
            await _runner.ShowUIAsync();
            return new Empty();
        }

        public override async Task<Empty> OpenProjects(Grpc_ProjectPaths request, ServerCallContext context) {
            try { await _runner.OpenProjectsAsync(request.Paths); }
            catch (ArgumentException ex) { throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message)); }
            return new Empty();
        }

        public override Task<Grpc_ActivationHost> GetActivationHost(Empty _, ServerCallContext context) =>
            Task.FromResult(new Grpc_ActivationHost { ProcessId = Environment.ProcessId });

        public override Task<Grpc_ProjectOpenRequest> GetPendingProjectOpen(Empty _, ServerCallContext context) {
            var response = new Grpc_ProjectOpenRequest();
            if (_runner.GetPendingProjectOpen() is { } request) {
                response.Id = request.Id;
                response.Paths.AddRange(request.Paths);
            }
            return Task.FromResult(response);
        }

        public override Task<Empty> CompleteProjectOpen(Grpc_ProjectOpenId request, ServerCallContext context) {
            _runner.CompleteProjectOpen(request.Id);
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> CloseUI(Empty _, ServerCallContext context) {
            _runner.CloseUI();
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> NotifyUIClosing(Grpc_UIClosing request, ServerCallContext context) {
            _runner.NotifyUIClosing(request.ProcessId);
            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> RestartUI(Empty _, ServerCallContext context) {
            await _runner.RestartUIAsync();
            return new Empty();
        }

        public override Task<Empty> ShowDebugView(Empty _, ServerCallContext context) {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new ThreadStart(delegate {
                    App.Services.GetRequiredService<DebugLog>().Show();
                }));

            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> ShutDown(Empty _, ServerCallContext context) {
            App.ShutDownAsync();

            return new Empty();
        }

        public override Task<Empty> RequestInstall(Empty request, ServerCallContext context) {
            // 检查是否有 pending installer update
            var installerFlagPath = Common.Constants.CommonPaths.InstallerUpdateFlagPath;
            if (File.Exists(installerFlagPath)) {
                // Installer update: 调用 ShutDownAsync 通过双向流请求 UI 关闭
                _ = App.ShutDownAsync();
            }
            else {
                // Plugin update: 直接发送关闭命令
                _runner.SendCloseCmd();
            }

            return Task.FromResult(new Empty());
        }

        public override async Task SubscribeUIRecievedCmd(Empty request, IServerStreamWriter<Grpc_UIRecievedCmd> responseStream, ServerCallContext context) {
            var messages = System.Threading.Channels.Channel.CreateUnbounded<MessageType>(new UnboundedChannelOptions { SingleReader = true });
            void Receive(object? sender, MessageType message) => messages.Writer.TryWrite(message);
            _runner.UISendCmd += Receive;
            try {
                // Subscribe first, then cover requests that arrived before this connection.
                if (_runner.GetPendingProjectOpen() != null) messages.Writer.TryWrite(MessageType.cmd_open_projects);
                // Stay subscribed while a pipe write is in flight, so consecutive opens are not lost.
                await foreach (var message in messages.Reader.ReadAllAsync(context.CancellationToken)) {
                    await responseStream.WriteAsync(new() {
                        IpcMsg = (int)message,
                    });
                }
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested) { }
            catch (Exception e) {
                App.Log.Error(e);
            }
            finally {
                _runner.UISendCmd -= Receive;
                messages.Writer.TryComplete();
            }
        }

        private readonly IUIRunnerService _runner = runner;
    }
}
