using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using NLog;
using System.Collections.ObjectModel;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.MonitorManager;
using VirtualPaper.Models.Cores.Interfaces;
using Monitor = VirtualPaper.Models.Cores.Monitor;
using Rectangle = System.Drawing.Rectangle;

namespace VirtualPaper.Grpc.Client
{
    public class MonitorManagerClient : IMonitorManagerClient
    {

        public event EventHandler? MonitorChanged;

        public ReadOnlyCollection<IMonitor> Monitors => _monitors.AsReadOnly();

        public IMonitor PrimaryMonitor { get; private set; }

        public Rectangle VirtulScreenBounds { get; private set; }

        public MonitorManagerClient()
        {
            _client = new MonitorManagerService.MonitorManagerServiceClient(new NamedPipeChannel(".", Constants.SingleInstance.GrpcPipeServerName));
           
            Task.Run(async () =>
            {
                _monitors.AddRange(await GetMonitorsAsync().ConfigureAwait(false));
                VirtulScreenBounds = await GetVirtualScreenBounds().ConfigureAwait(false);
                PrimaryMonitor = _monitors.FirstOrDefault(x => x.IsPrimary);
            }).Wait();

            _cancellationTokeneMonitorChanged = new CancellationTokenSource();
            _monitorChangedTask = Task.Run(() => SubscribeMonitorChangedStream(_cancellationTokeneMonitorChanged.Token));
        }

        private async Task<IEnumerable<IMonitor>> GetMonitorsAsync()
        {
            var resp = await _client.GetMonitorsAsync(new Empty());
            List<IMonitor> monitors = [];
            for (int i = 0; i < resp.Monitors.Count; i++) 
            {
                var monitor = resp.Monitors[i];
                monitors.Add(new Monitor()
                {
                    DeviceId = monitor.DeviceId,
                    DeviceName = monitor.DeviceName,
                    MonitorName = monitor.DisplayName,
                    HMonitor = monitor.HMonitor,
                    Content = (i + 1).ToString(),
                    IsPrimary = monitor.IsPrimary,
                    Bounds = new Rectangle(
                        monitor.Bounds.X,
                        monitor.Bounds.Y,
                        monitor.Bounds.Width,
                        monitor.Bounds.Height),
                    WorkingArea = new Rectangle(
                        monitor.WorkingArea.X,
                        monitor.WorkingArea.Y,
                        monitor.WorkingArea.Width,
                        monitor.WorkingArea.Height),
                    ThumbnailPath = monitor.ThumbnailPath,
                });
            }

            return monitors;
        }

        public async Task IdentifyMonitorsAsync()
        {
            await _client.IdentifyMonitorsAsync(new Empty());
        }

        private async Task<Rectangle> GetVirtualScreenBounds()
        {
            var resp = await _client.GetVirtualScreenBoundsAsync(new Empty());
            var vsb = new Rectangle(
                        resp.X,
                        resp.Y,
                        resp.Width,
                        resp.Height);
            return vsb;
        }

        private async Task SubscribeMonitorChangedStream(CancellationToken token)
        {
            try
            {
                using var call = _client.SubscribeMonitorChanged(new Empty());
                while (await call.ResponseStream.MoveNext(token))
                {
                    await _monitorChangedLock.WaitAsync();
                    try
                    {
                        var response = call.ResponseStream.Current;

                        _monitors.Clear();
                        _monitors.AddRange(await GetMonitorsAsync().ConfigureAwait(false));
                        VirtulScreenBounds = await GetVirtualScreenBounds().ConfigureAwait(false);
                        PrimaryMonitor = _monitors.FirstOrDefault(x => x.IsPrimary);
                        MonitorChanged?.Invoke(this, EventArgs.Empty);
                    }
                    finally
                    {
                        _monitorChangedLock.Release();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _cancellationTokeneMonitorChanged?.Cancel();
                    _monitorChangedTask?.Wait();
                }
                
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private MonitorManagerService.MonitorManagerServiceClient _client;
        private readonly List<IMonitor> _monitors = [];
        private readonly SemaphoreSlim _monitorChangedLock = new(1, 1);
        private readonly CancellationTokenSource _cancellationTokeneMonitorChanged;
        private readonly Task _monitorChangedTask;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
