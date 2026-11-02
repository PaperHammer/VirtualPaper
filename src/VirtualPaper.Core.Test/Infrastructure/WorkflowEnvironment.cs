using System.Collections.ObjectModel;
using System.Drawing;
using GrpcDotNetNamedPipes;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Grpc.Service.UserSettings;
using VirtualPaper.GrpcServers;
using VirtualPaper.Models.Cores;
using VirtualPaper.Services;
using VirtualPaper.Services.Interfaces;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.Core.Test.Infrastructure;

// Tests using this environment must be DoNotParallelize: CommonPaths is process-wide.
internal sealed class WorkflowEnvironment : IDisposable {
    private readonly bool _previousTestMode = Constants.IsTestMode;
    private readonly string _previousRoot = Constants.CommonPaths.TestRootDir;
    private NamedPipeServer? _server;

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "VirtualPaper.Workflows", Guid.NewGuid().ToString("N"));
    public string PipeName { get; } = $"VirtualPaper.Workflow.{Guid.NewGuid():N}";
    public Mock<IMonitorManager> Monitors { get; } = new();
    public Mock<IUIRunnerService> UiRunner { get; } = new();
    public UserSettingsService Settings { get; private set; } = null!;
    public NamedPipeChannel CreateChannel() => new(".", PipeName);

    public WorkflowEnvironment() {
        Constants.IsTestMode = true;
        Constants.CommonPaths.TestRootDir = Root;
        Directory.CreateDirectory(Root);
        var primary = new Monitor {
            DeviceId = "WORKFLOW-DISPLAY-1", Content = "Workflow display", IsPrimary = true,
            Bounds = new Rectangle(0, 0, 1920, 1080), WorkingArea = new Rectangle(0, 0, 1920, 1040)
        };
        Monitors.SetupGet(m => m.PrimaryMonitor).Returns(primary);
        Monitors.SetupGet(m => m.Monitors).Returns(new ObservableCollection<Monitor> { primary });
        Monitors.Setup(m => m.MonitorExists(It.IsAny<VirtualPaper.Models.Cores.Interfaces.IMonitor>())).Returns(true);
    }

    public void StartSettingsServer(Action<NamedPipeServer>? bindServices = null) {
        _server?.Kill();
        _server?.Dispose();
        Settings = new UserSettingsService(Monitors.Object, new JsonSaverWrapper(), _ => { });
        _server = new NamedPipeServer(PipeName);
        Grpc_UserSettingsService.BindService(_server.ServiceBinder,
            new UserSettingServer(Monitors.Object, Settings, UiRunner.Object));
        bindServices?.Invoke(_server);
        _server.Start();
    }

    public void Dispose() {
        try {
            _server?.Kill();
            _server?.Dispose();
            Directory.Delete(Root, recursive: true);
        }
        finally {
            Constants.CommonPaths.TestRootDir = _previousRoot;
            Constants.IsTestMode = _previousTestMode;
        }
    }
}
