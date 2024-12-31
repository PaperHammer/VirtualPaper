using System.Windows.Threading;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Grpc.Service.MonitorManager;
using VirtualPaper.Views;
using Application = System.Windows.Application;

namespace VirtualPaper.GrpcServers {
    internal class MonitorManagerServer(
        IMonitorManager monitorManager) : Grpc_MonitorManagerService.Grpc_MonitorManagerServiceBase {
        public override Task<Grpc_GetMonitorsResponse> GetMonitors(Empty _, ServerCallContext context) {
            var resp = new Grpc_GetMonitorsResponse();

            foreach (var monitor in _monitorManager.Monitors) {
                var item = new Grpc_MonitorData() {
                    DeviceId = monitor.DeviceId,
                    IsPrimary = monitor.IsPrimary,
                    Bounds = new() {
                        X = monitor.Bounds.X,
                        Y = monitor.Bounds.Y,
                        Width = monitor.Bounds.Width,
                        Height = monitor.Bounds.Height
                    },
                    WorkingArea = new() {
                        X = monitor.WorkingArea.X,
                        Y = monitor.WorkingArea.Y,
                        Width = monitor.WorkingArea.Width,
                        Height = monitor.WorkingArea.Height
                    },
                    ThumbnailPath = monitor.ThumbnailPath,
                };
                resp.Monitors.Add(item);
            }

            return Task.FromResult(resp);
        }

        public override Task<Grpc_Rectangle> GetVirtualScreenBounds(Empty _, ServerCallContext context) {
            var resp = new Grpc_Rectangle() {
                X = _monitorManager.VirtualScreenBounds.X,
                Y = _monitorManager.VirtualScreenBounds.Y,
                Width = _monitorManager.VirtualScreenBounds.Width,
                Height = _monitorManager.VirtualScreenBounds.Height,
            };

            return Task.FromResult(resp);
        }

        public override async Task<Empty> IdentifyMonitors(Empty request, ServerCallContext context) {
            await _indentifySlim.WaitAsync();

            try {
                int cnt = _monitorManager.Monitors.Count;
                List<Task> tasks = [];
                for (int i = 0; i < cnt; ++i) {
                    int monitorIndex = i; // 避免闭包问题
                    tasks.Add(Task.Run(async () => {
                        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () => {
                            var monitor = _monitorManager.Monitors[monitorIndex];
                            IdentifyWindow identifyWindow = new(monitorIndex + 1) {
                                Owner = App.Current.MainWindow,
                                Left = monitor.WorkingArea.Left,
                                Top = monitor.WorkingArea.Top,
                            };
                            identifyWindow.Show();
                            await Task.Delay(2000);
                            identifyWindow.Close();
                        }));
                    }));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception e) {
                App.Log.Error(e);
            }
            finally {
                _indentifySlim.Release();
            }

            return new Empty();
        }

        public override async Task SubscribeMonitorChanged(Empty _, IServerStreamWriter<Empty> responseStream, ServerCallContext context) {
            try {
                while (!context.CancellationToken.IsCancellationRequested) {
                    var tcs = new TaskCompletionSource<bool>();
                    _monitorManager.MonitorUpdated += MonitorChanged;
                    void MonitorChanged(object? s, EventArgs e) {
                        _monitorManager.MonitorUpdated -= MonitorChanged;
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested) {
                        _monitorManager.MonitorUpdated -= MonitorChanged;
                        break;
                    }

                    await responseStream.WriteAsync(new Empty());
                }
            }
            catch (Exception e) {
                App.Log.Error(e);
            }
        }

        public override async Task SubscribeMonitorPropertyChanged(Empty _, IServerStreamWriter<Empty> responseStream, ServerCallContext context) {
            try {
                while (!context.CancellationToken.IsCancellationRequested) {
                    var tcs = new TaskCompletionSource<bool>();
                    _monitorManager.MonitorPropertyUpdated += MonitorPropertyChanged;
                    void MonitorPropertyChanged(object? s, EventArgs e) {
                        _monitorManager.MonitorPropertyUpdated -= MonitorPropertyChanged;
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested) {
                        _monitorManager.MonitorPropertyUpdated -= MonitorPropertyChanged;
                        break;
                    }

                    await responseStream.WriteAsync(new Empty());
                }
            }
            catch (Exception e) {
                App.Log.Error(e);
            }
        }

        private readonly IMonitorManager _monitorManager = monitorManager;
        private static readonly SemaphoreSlim _indentifySlim = new(1, 1);
    }
}
