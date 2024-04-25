using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Windows.Threading;
using VirtualPaper.Grpc.Service.Commands;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Views;
using Application = System.Windows.Application;

namespace VirtualPaper.GrpcServers
{
    internal class CommandsServer : CommandsService.CommandsServiceBase
    {
        public CommandsServer(
            IUIRunnerService runner, 
            IScreensaverService screensaver)
        {
            this._runner = runner;
            this._screensaver = screensaver;
        }

        public override Task<Empty> ShowUI(Empty _, ServerCallContext context)
        {
            _runner.ShowUI();
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> CloseUI(Empty _, ServerCallContext context)
        {
            _runner.CloseUI();
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> RestartUI(Empty _, ServerCallContext context)
        {
            _runner.RestartUI();
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> ShowDebugView(Empty _, ServerCallContext context)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ThreadStart(delegate
            {
                if (_debugLogWindow == null)
                {
                    _debugLogWindow = new DebugLog();
                    _debugLogWindow.Closed += (s, e) => _debugLogWindow = null;
                    _debugLogWindow.Show();
                }
            }));
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> Screensaver(ScreensaverRequest request, ServerCallContext context)
        {
            switch (request.State)
            {
                case ScreensaverState.Start:
                    _screensaver.Start();
                    break;
                case ScreensaverState.Stop:
                    _screensaver.Stop();
                    break;
                case ScreensaverState.Preview:
                    _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ThreadStart(delegate
                    {
                        _screensaver.CreatePreview(new IntPtr(request.PreviewHwnd));
                    }));
                    break;
                case ScreensaverState.Configure:
                    //TODO: Open control panel or cutomise panel
                    _runner.ShowUI();
                    break;
            }
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> ShutDown(Empty _, ServerCallContext context)
        {
            try
            {
                return Task.FromResult(new Empty());
            }
            finally
            {
                App.ShutDown();
            }
        }

        public override Task<Empty> SaveRectUI(Empty _, ServerCallContext context)
        {
            _runner.SaveRectUI();
            return Task.FromResult(new Empty());
        }

        private DebugLog _debugLogWindow;
        private readonly IUIRunnerService _runner;
        private readonly IScreensaverService _screensaver;
    }
}
