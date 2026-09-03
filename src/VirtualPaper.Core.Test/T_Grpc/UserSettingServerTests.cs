using System.Collections.ObjectModel;
using System.Drawing;
using Google.Protobuf.WellKnownTypes;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Grpc.Service.UserSettings;
using VirtualPaper.GrpcServers;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.EditPanel;
using VirtualPaper.Services.Interfaces;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.Core.Test.T_Grpc;

[TestClass]
public class UserSettingServerTests {
    [TestMethod]
    public async Task GetRecentUseds_MapsEveryFieldWithoutMutatingSource() {
        var fixture = new ServerFixture();
        fixture.RecentUseds.Add(new RecentUsed(
            FileType.FWebDesign,
            "site",
            @"C:\projects\site.vwpweb",
            "2026-09-03"));

        Grpc_RecentUseds response = await fixture.Server.GetRecentUseds(new Empty(), null!);

        Assert.HasCount(1, response.RecentUseds);
        Assert.AreEqual((int)FileType.FWebDesign, (int)response.RecentUseds[0].FType);
        Assert.AreEqual("site", response.RecentUseds[0].FileName);
        Assert.AreEqual(@"C:\projects\site.vwpweb", response.RecentUseds[0].FilePath);
        Assert.AreEqual("2026-09-03", response.RecentUseds[0].DateTime);
        Assert.HasCount(1, fixture.RecentUseds);
    }

    [TestMethod]
    public async Task SetRecentUseds_ReplacesCollectionAndPersistsIt() {
        var fixture = new ServerFixture();
        fixture.RecentUseds.Add(new RecentUsed(FileType.FImage, "old", "old", "old"));
        var request = new Grpc_RecentUseds();
        request.RecentUseds.Add(new Grpc_RecentUsed {
            FType = (Grpc_FileType)(int)FileType.FDesign,
            FileName = "new",
            FilePath = @"C:\new.vwp",
            DateTime = "now",
        });

        await fixture.Server.SetRecentUseds(request, null!);

        IRecentUsed item = AssertSingle(fixture.RecentUseds);
        Assert.AreEqual(FileType.FDesign, item.Type);
        Assert.AreEqual("new", item.FileName);
        Assert.AreEqual(@"C:\new.vwp", item.FilePath);
        fixture.UserSettings.Verify(service => service.Save<List<IRecentUsed>>(), Times.Once);
    }

    [TestMethod]
    public async Task WallpaperLayoutsAndAppRules_RoundTripGrpcFields() {
        var fixture = new ServerFixture();
        fixture.WallpaperLayouts.Add(new WallpaperLayout("folder", "monitor", "content", "RWeb"));
        fixture.AppRules.Add(new ApplicationRules("game.exe", AppWpRunRulesEnum.Pause));

        Grpc_WallpaperLayoutsSettings layouts =
            await fixture.Server.GetWallpaperLayouts(new Empty(), null!);
        Grpc_AppRulesSettings rules =
            await fixture.Server.GetAppRulesSettings(new Empty(), null!);

        Assert.HasCount(1, layouts.WallpaperLayouts);
        Assert.AreEqual("folder", layouts.WallpaperLayouts[0].FolderPath);
        Assert.AreEqual("monitor", layouts.WallpaperLayouts[0].MonitorDeviceId);
        Assert.AreEqual("content", layouts.WallpaperLayouts[0].MonitorContent);
        Assert.AreEqual("RWeb", layouts.WallpaperLayouts[0].RType);
        Assert.HasCount(1, rules.AppRules);
        Assert.AreEqual("game.exe", rules.AppRules[0].AppName);
        Assert.AreEqual((int)AppWpRunRulesEnum.Pause, (int)rules.AppRules[0].Rule);
    }

    [TestMethod]
    public async Task SetAppRules_ReplacesCollectionAndPersistsIt() {
        var fixture = new ServerFixture();
        fixture.AppRules.Add(new ApplicationRules("old.exe", AppWpRunRulesEnum.KeepRun));
        var request = new Grpc_AppRulesSettings();
        request.AppRules.Add(new Grpc_AppRulesData {
            AppName = "new.exe",
            Rule = (Grpc_AppRulesEnum)(int)AppWpRunRulesEnum.Silence,
        });

        await fixture.Server.SetAppRulesSettings(request, null!);

        IApplicationRules item = AssertSingle(fixture.AppRules);
        Assert.AreEqual("new.exe", item.AppName);
        Assert.AreEqual(AppWpRunRulesEnum.Silence, item.Rule);
        fixture.UserSettings.Verify(service => service.Save<List<IApplicationRules>>(), Times.Once);
    }

    [TestMethod]
    public async Task GetSettings_MapsMonitorGeometryAndScreenSaverProcessList() {
        var selected = new Monitor {
            DeviceId = "DISPLAY-2",
            IsPrimary = false,
            Content = "Secondary",
            Bounds = new Rectangle(100, 200, 1920, 1080),
            WorkingArea = new Rectangle(100, 200, 1920, 1040),
        };
        var fixture = new ServerFixture(selected);
        fixture.Settings.AppName = "VirtualPaper Test";
        fixture.Settings.WallpaperWaitTime = 1234;
        fixture.Settings.WhiteListScr.Add(new ProcInfo {
            ProcName = "allowed.exe",
            IconPath = "icon.png",
            IsRunning = true,
        });

        Grpc_SettingsData response = await fixture.Server.GetSettings(new Empty(), null!);

        Assert.AreEqual("VirtualPaper Test", response.AppName);
        Assert.AreEqual(1234, response.WallpaperWaitTime);
        Assert.AreEqual("DISPLAY-2", response.SelectedMonitor.DeviceId);
        Assert.AreEqual(100, response.SelectedMonitor.Bounds.X);
        Assert.AreEqual(1080, response.SelectedMonitor.Bounds.Height);
        Assert.AreEqual(1040, response.SelectedMonitor.WorkingArea.Height);
        Assert.HasCount(1, response.WhiteListScr);
        Assert.AreEqual("allowed.exe", response.WhiteListScr[0].ProcName);
        Assert.IsTrue(response.WhiteListScr[0].IsRunning);
    }

    [TestMethod]
    public async Task SetSettings_SelectsKnownMonitorPersistsAndRestartsForBackdropChange() {
        var primary = new Monitor { DeviceId = "DISPLAY-1", IsPrimary = true };
        var secondary = new Monitor { DeviceId = "DISPLAY-2" };
        var fixture = new ServerFixture(primary, secondary);
        fixture.Settings.Language = "zh-CN";
        fixture.Settings.ApplicationTheme = AppTheme.Auto;
        fixture.Settings.SystemBackdrop = AppSystemBackdrop.Default;
        fixture.Settings.IsAutoStart = false;
        var request = new Grpc_SettingsData {
            SelectedMonitor = new Grpc_MonitorData { DeviceId = "DISPLAY-2" },
            AppName = "updated",
            AppVersion = "2.0",
            Language = "zh-CN",
            ApplicationTheme = (Grpc_AppTheme)(int)AppTheme.Auto,
            SystemBackdrop = (Grpc_AppSystemBackdrop)(int)AppSystemBackdrop.Mica,
            WallpaperArrangement = (Grpc_WallpaperArrangement)(int)WallpaperArrangement.Expand,
            WallpaperWaitTime = 2500,
            IsAutoStart = false,
        };
        request.WhiteListScr.Add(new Grpc_ProcInfoData {
            ProcName = "player.exe",
            IconPath = "player.png",
            IsRunning = true,
        });

        await fixture.Server.SetSettings(request, null!);

        Assert.AreSame(secondary, fixture.Settings.SelectedMonitor);
        Assert.AreEqual("updated", fixture.Settings.AppName);
        Assert.AreEqual(WallpaperArrangement.Expand, fixture.Settings.WallpaperArrangement);
        Assert.AreEqual(AppSystemBackdrop.Mica, fixture.Settings.SystemBackdrop);
        Assert.AreEqual(2500, fixture.Settings.WallpaperWaitTime);
        Assert.AreEqual("player.exe", AssertSingle(fixture.Settings.WhiteListScr).ProcName);
        fixture.UserSettings.Verify(service => service.Save<ISettings>(), Times.Once);
        fixture.UiRunner.Verify(service => service.RestartUI(), Times.Once);
    }

    private static T AssertSingle<T>(IEnumerable<T> values) {
        T[] items = values.ToArray();
        Assert.HasCount(1, items);
        return items[0];
    }

    private sealed class ServerFixture {
        public Settings Settings { get; } = new();
        public List<IRecentUsed> RecentUseds { get; } = [];
        public List<IApplicationRules> AppRules { get; } = [];
        public List<IWallpaperLayout> WallpaperLayouts { get; } = [];
        public Mock<IUserSettingsService> UserSettings { get; } = new();
        public Mock<IUIRunnerService> UiRunner { get; } = new();
        public UserSettingServer Server { get; }

        public ServerFixture(params Monitor[] monitors) {
            if (monitors.Length == 0) {
                monitors = [new Monitor { DeviceId = "DISPLAY-1", IsPrimary = true }];
            }
            Settings.SelectedMonitor = monitors[0];
            UserSettings.SetupGet(service => service.Settings).Returns(Settings);
            UserSettings.SetupGet(service => service.RecentUseds).Returns(RecentUseds);
            UserSettings.SetupGet(service => service.AppRules).Returns(AppRules);
            UserSettings.SetupGet(service => service.WallpaperLayouts).Returns(WallpaperLayouts);

            var monitorManager = new Mock<IMonitorManager>();
            monitorManager.SetupGet(manager => manager.Monitors)
                .Returns(new ObservableCollection<Monitor>(monitors));
            monitorManager.SetupGet(manager => manager.PrimaryMonitor).Returns(monitors[0]);

            Server = new UserSettingServer(monitorManager.Object, UserSettings.Object, UiRunner.Object);
        }
    }
}
