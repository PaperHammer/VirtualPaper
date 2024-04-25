using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NLog;
using System.Windows.Threading;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Grpc.Service.MonitorManager;
using VirtualPaper.Views;
using Application = System.Windows.Application;
using Rectangle = VirtualPaper.Grpc.Service.MonitorManager.Rectangle;

namespace VirtualPaper.GrpcServers
{
    internal class MonitorManagerServer(
        IMonitorManager monitorManager) : MonitorManagerService.MonitorManagerServiceBase
    {
        public override Task<GetMonitorsResponse> GetMonitors(Empty _, ServerCallContext context)
        {
            var resp = new GetMonitorsResponse();

            try
            {
                foreach (var monitor in _monitorManager.Monitors)
                {
                    var item = new MonitorData()
                    {
                        DeviceId = monitor.DeviceId,
                        DeviceName = monitor.DeviceName,
                        DisplayName = monitor.DeviceName,
                        HMonitor = monitor.HMonitor.ToInt32(),
                        IsPrimary = monitor.IsPrimary,
                        Bounds = new()
                        {
                            X = monitor.Bounds.X,
                            Y = monitor.Bounds.Y,
                            Width = monitor.Bounds.Width,
                            Height = monitor.Bounds.Height
                        },
                        WorkingArea = new()
                        {
                            X = monitor.WorkingArea.X,
                            Y = monitor.WorkingArea.Y,
                            Width = monitor.WorkingArea.Width,
                            Height = monitor.WorkingArea.Height
                        }
                    };
                    resp.Monitors.Add(item);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }

            return Task.FromResult(resp);
        }

        public override Task<Rectangle> GetVirtualScreenBounds(Empty _, ServerCallContext context)
        {
            var resp = new Rectangle()
            {
                X = _monitorManager.VirtualScreenBounds.X,
                Y = _monitorManager.VirtualScreenBounds.Y,
                Width = _monitorManager.VirtualScreenBounds.Width,
                Height = _monitorManager.VirtualScreenBounds.Height,
            };

            return Task.FromResult(resp);
        }

        public override async Task<Empty> IdentifyMonitors(Empty request, ServerCallContext context)
        {
            int cnt = _monitorManager.Monitors.Count;
            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ThreadStart(async delegate
            {
                for (int i = 0; i < cnt; ++i)
                {
                    var monitor = _monitorManager.Monitors[i];
                    IdentifyWindow identifyWindow = new(i + 1)
                    {
                        Owner = App.Current.MainWindow,
                        Left = monitor.WorkingArea.Left,
                        Top = monitor.WorkingArea.Top,
                    };
                    identifyWindow.Show();
                    await Task.Delay(1000);
                    identifyWindow.Close();
                }
            }));

            return new Empty();
        }

        public override async Task SubscribeMonitorChanged(Empty _, IServerStreamWriter<Empty> responseStream, ServerCallContext context)
        {
            try
            {
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _monitorManager.MonitorUpdated += MonitorChanged;
                    void MonitorChanged(object? s, EventArgs e)
                    {
                        _monitorManager.MonitorUpdated -= MonitorChanged;
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        _monitorManager.MonitorUpdated -= MonitorChanged;
                        break;
                    }

                    await responseStream.WriteAsync(new Empty());
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IMonitorManager _monitorManager = monitorManager;
    }
}
