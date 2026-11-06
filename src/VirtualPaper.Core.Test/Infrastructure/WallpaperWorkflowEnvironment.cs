using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Cores;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.DataAssistor;
using VirtualPaper.Factories;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.GrpcServers;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;

namespace VirtualPaper.Core.Test.Infrastructure;

internal sealed class WallpaperWorkflowEnvironment : IDisposable {
    public WorkflowEnvironment Environment { get; } = new();
    public WallpaperControl Control { get; private set; } = null!;
    public WallpaperControlClient Client { get; }
    public UserSettingsClient SettingsClient { get; }
    public Mock<IMonitorManagerClient> MonitorClient { get; } = new();
    public Mock<IGlobalDialogService> Dialog { get; } = new();
    public List<Mock<IWpPlayer>> Players { get; } = [];
    public bool FailPlayerStart { get; set; }
    public DialogResult DialogResult { get; set; } = DialogResult.Primary;
    public IMonitor Monitor => Environment.Monitors.Object.PrimaryMonitor;
    public string SourcePath { get; }
    public byte[] SourceHash { get; }

    public WallpaperWorkflowEnvironment() {
        CrossThreadInvoker.Initialize(new InlineContext());
        Environment.StartSettingsServer(server => {
            Environment.Settings.Settings.WallpaperDir = Path.Combine(Environment.Root, "library");
            Environment.Settings.Settings.BuiltInWallpapersDir = Path.Combine(Environment.Root, "curated wallpapers");
            Environment.Settings.Settings.WallpaperArrangement = WallpaperArrangement.Per;
            Directory.CreateDirectory(Environment.Settings.Settings.WallpaperDir);
            Environment.Settings.Save<ISettings>();
            var arguments = new WallpaperFactory(Environment.Settings);
            var factory = new Mock<IWallpaperFactory>();
            factory.Setup(f => f.CreatePlayerStartArgs(It.IsAny<IWpPlayerData>(), It.IsAny<bool>(), It.IsAny<string?>()))
                .Returns((IWpPlayerData data, bool preview, string? metadata) => arguments.CreatePlayerStartArgs(data, preview, metadata));
            factory.Setup(f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), It.IsAny<IMonitor>(), It.IsAny<bool>()))
                .Returns((IWpPlayerData initial, IMonitor monitor, bool preview) => {
                    var current = initial;
                    var player = TestDataBuilder.CreateWpPlayer(initial, monitor);
                    player.SetupGet(p => p.Data).Returns(() => current);
                    player.SetupGet(p => p.StartArgs).Returns(() => arguments.CreatePlayerStartArgs(current, preview)!);
                    player.Setup(p => p.Update(It.IsAny<IWpPlayerData>())).Callback<IWpPlayerData>(data => current = data);
                    player.Setup(p => p.ShowAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(ct => {
                        ct.ThrowIfCancellationRequested();
                        return Task.FromResult(!FailPlayerStart);
                    });
                    Players.Add(player);
                    return player.Object;
                });
            Control = new WallpaperControl(Environment.Settings, Environment.Monitors.Object, factory.Object,
                MockFactory.CreateDesktopService(workerWValid: false).Object, Mock.Of<IJobService>());
            Grpc_WallpaperControlService.BindService(server.ServiceBinder,
                new WallpaperControlServer(Control, Environment.Monitors.Object, Environment.Settings));
        });
        Client = new WallpaperControlClient(Environment.CreateChannel());
        SettingsClient = new UserSettingsClient(Environment.CreateChannel());
        MonitorClient.SetupGet(m => m.PrimaryMonitor).Returns(Monitor);
        MonitorClient.SetupGet(m => m.Monitors).Returns(() => Environment.Monitors.Object.Monitors.Cast<IMonitor>().ToList().AsReadOnly());
        Dialog.Setup(d => d.ShowDialogAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(DialogResult));
        SourcePath = Path.Combine(Environment.Root, "source.png");
        using (var bitmap = new Bitmap(48, 64)) {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.SteelBlue);
            bitmap.Save(SourcePath, ImageFormat.Png);
        }
        SourceHash = SHA256.HashData(File.ReadAllBytes(SourcePath));
    }

    public async Task<IWpBasicData> ImportAsync() {
        var imported = await Client.CreateBasicDataAsync(SourcePath, FileType.FImage, CancellationToken.None);
        Assert.IsNotNull(imported);
        return DataAssist.GrpcToBasicData(imported);
    }

    public async Task<(LibraryContentsViewModel Library, WallpaperIndexService Index)> OpenLibraryAsync() {
        var index = new WallpaperIndexService();
        var settings = new WpSettingsViewModel(MonitorClient.Object, Client, SettingsClient, Mock.Of<IStoragePicker>());
        var library = new LibraryContentsViewModel(SettingsClient, Client, settings, index, Dialog.Object);
        await index.Initialized.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Seed the bound collection from the real index without loading a XAML page/loading overlay.
        library.TestPopulate(index.Query(0, int.MaxValue)
            .Select(e => WpBasicData.Load(e.JsonPath)));
        return (library, index);
    }

    public void Dispose() {
        Client.Dispose();
        SettingsClient.Dispose();
        Control.Dispose();
        Environment.Dispose();
    }

    private sealed class InlineContext : IUiSynchronizationContext {
        public override void Post(Action action) => action();
        public override void Send(Action action) => action();
        public override Task PostAsync(Func<Task> action) => action();
        public override Task<T> PostAsync<T>(Func<Task<T>> action) => action();
    }
}
