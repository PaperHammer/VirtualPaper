using VirtualPaper.Common;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.Core.Test.T_DataAssist;

/// <summary>
/// DataAssist 映射函数专项测试。
/// 测试策略：往返（roundtrip）验证 —— 原始对象 → Grpc 对象 → 还原对象，
/// 所有字段应完整保留。同时覆盖枚举边界值映射。
/// </summary>
[TestClass]
public class DataAssistMappingTests {

    // ════════════════════════════════════════════════════════════════════════
    // 1. WpBasicData  ↔  Grpc_WpBasicData
    // ════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void BasicData_Roundtrip_PreservesAllStringFields() {
        var src = MakeBasicData();

        var grpc = DataAssist.BasicDataToGrpcData(src);
        var restored = DataAssist.GrpcToBasicData(grpc);

        Assert.AreEqual(src.WallpaperUid, restored.WallpaperUid);
        Assert.AreEqual(src.Title, restored.Title);
        Assert.AreEqual(src.Desc, restored.Desc);
        Assert.AreEqual(src.Authors, restored.Authors);
        Assert.AreEqual(src.PublishDate, restored.PublishDate);
        Assert.AreEqual(src.Partition, restored.Partition);
        Assert.AreEqual(src.Tags, restored.Tags);
        Assert.AreEqual(src.FolderName, restored.FolderName);
        Assert.AreEqual(src.FolderPath, restored.FolderPath);
        Assert.AreEqual(src.FilePath, restored.FilePath);
        Assert.AreEqual(src.ThumbnailPath, restored.ThumbnailPath);
        Assert.AreEqual(src.Resolution, restored.Resolution);
        Assert.AreEqual(src.AspectRatio, restored.AspectRatio);
        Assert.AreEqual(src.FileExtension, restored.FileExtension);
    }

    [TestMethod]
    public void BasicData_Roundtrip_PreservesNumericAndBoolFields() {
        var src = MakeBasicData();

        var grpc = DataAssist.BasicDataToGrpcData(src);
        var restored = DataAssist.GrpcToBasicData(grpc);

        Assert.AreEqual(src.Rating, restored.Rating);
        Assert.AreEqual(src.FileSize, restored.FileSize);
        Assert.AreEqual(src.IsSingleRType, restored.IsSingleRType);
    }

    [TestMethod]
    public void BasicData_Roundtrip_PreservesAppInfo() {
        var src = MakeBasicData();

        var grpc = DataAssist.BasicDataToGrpcData(src);
        var restored = DataAssist.GrpcToBasicData(grpc);

        Assert.AreEqual(src.AppInfo.AppName, restored.AppInfo.AppName);
        Assert.AreEqual(src.AppInfo.AppVersion, restored.AppInfo.AppVersion);
        Assert.AreEqual(src.AppInfo.FileVersion, restored.AppInfo.FileVersion);
    }

    [TestMethod]
    public void BasicData_Roundtrip_PreservesFileTypeEnum() {
        foreach (FileType ftype in Enum.GetValues<FileType>()) {
            var src = MakeBasicData(ftype: ftype);

            var grpc = DataAssist.BasicDataToGrpcData(src);
            var restored = DataAssist.GrpcToBasicData(grpc);

            Assert.AreEqual(ftype, restored.FType, $"FileType.{ftype} roundtrip failed");
        }
    }

    /// <summary>
    // ════════════════════════════════════════════════════════════════════════
    // 2. WpRuntimeData  ↔  Grpc_WpRuntimeData
    // ════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void RuntimeData_Roundtrip_PreservesAllFields() {
        var src = new WpRuntimeData {
            MonitorContent = "DISPLAY1",
            FolderPath = @"C:\wallpapers\001",
            DepthFilePath = @"C:\wallpapers\001\depth.png",
            WpEffectFilePathTemplate = @"C:\effects\template.json",
            WpEffectFilePathTemporary = @"C:\effects\temp.json",
            WpEffectFilePathUsing = @"C:\effects\using.json",
            RType = RuntimeType.RImage,
            AppInfo = new ApplicationInfo {
                AppName = "VirtualPaper",
                AppVersion = "2.0.0",
                FileVersion = "2.0.0.1",
            },
        };

        var grpc = DataAssist.RuntimeDataToGrpcData(src);
        var restored = DataAssist.GrpcToRuntimeData(grpc);

        Assert.AreEqual(src.MonitorContent, restored.MonitorContent);
        Assert.AreEqual(src.FolderPath, restored.FolderPath);
        Assert.AreEqual(src.DepthFilePath, restored.DepthFilePath);
        Assert.AreEqual(src.WpEffectFilePathTemplate, restored.WpEffectFilePathTemplate);
        Assert.AreEqual(src.WpEffectFilePathTemporary, restored.WpEffectFilePathTemporary);
        Assert.AreEqual(src.WpEffectFilePathUsing, restored.WpEffectFilePathUsing);
        Assert.AreEqual(src.RType, restored.RType);
        Assert.AreEqual(src.AppInfo.AppName, restored.AppInfo.AppName);
        Assert.AreEqual(src.AppInfo.AppVersion, restored.AppInfo.AppVersion);
        Assert.AreEqual(src.AppInfo.FileVersion, restored.AppInfo.FileVersion);
    }

    [TestMethod]
    public void RuntimeData_Roundtrip_PreservesRuntimeTypeEnum() {
        foreach (RuntimeType rtype in Enum.GetValues<RuntimeType>()) {
            var src = new WpRuntimeData { RType = rtype, AppInfo = new ApplicationInfo() };

            var grpc = DataAssist.RuntimeDataToGrpcData(src);
            var restored = DataAssist.GrpcToRuntimeData(grpc);

            Assert.AreEqual(rtype, restored.RType, $"RuntimeType.{rtype} roundtrip failed");
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3. MetadataToGrpcPlayingData  +  GrpcToPlayerData
    // ════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void PlayerData_MetadataToGrpc_PreservesKeyFields() {
        var src = MakeBasicData();
        var rtype = RuntimeType.RVideo;

        var grpc = DataAssist.MetadataToGrpcPlayingData(src, rtype);

        Assert.AreEqual(src.WallpaperUid, grpc.WallpaperUid);
        Assert.AreEqual(src.FilePath, grpc.FilePath);
        Assert.AreEqual(src.FolderPath, grpc.FolderPath);
        Assert.AreEqual(src.ThumbnailPath, grpc.ThumbnailPath);
        Assert.AreEqual((Grpc_RuntimeType)rtype, grpc.RType);
    }

    [TestMethod]
    public void PlayerData_GrpcToPlayerData_PreservesAllFields() {
        var grpc = new Grpc_WpPlayerData {
            WallpaperUid = "uid-player-01",
            RType = Grpc_RuntimeType.RImage,
            FilePath = @"C:\wp\file.jpg",
            FolderPath = @"C:\wp",
            ThumbnailPath = @"C:\wp\thumb.jpg",
            DepthFilePath = @"C:\wp\depth.png",
        };

        var player = DataAssist.GrpcToPlayerData(grpc);

        Assert.AreEqual(grpc.WallpaperUid, player.WallpaperUid);
        Assert.AreEqual(grpc.FilePath, player.FilePath);
        Assert.AreEqual(grpc.FolderPath, player.FolderPath);
        Assert.AreEqual(grpc.ThumbnailPath, player.ThumbnailPath);
        Assert.AreEqual(grpc.DepthFilePath, player.DepthFilePath);
        Assert.AreEqual(RuntimeType.RImage, player.RType);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4. Monitor  ↔  Grpc_MonitorData
    // ════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Monitor_GrpcToMonitor_PreservesAllFields() {
        var grpc = new Grpc_MonitorData {
            DeviceId = @"\\.\DISPLAY1",
            Content = "Primary",
            IsPrimary = true,
            ThumbnailPath = @"C:\thumb.png",
            Bounds = new Grpc_Rectangle { X = 0, Y = 0, Width = 1920, Height = 1080 },
            WorkingArea = new Grpc_Rectangle { X = 0, Y = 40, Width = 1920, Height = 1040 },
        };

        var monitor = DataAssist.GrpToMonitorData(grpc);

        Assert.AreEqual(grpc.DeviceId, monitor.DeviceId);
        Assert.AreEqual(grpc.Content, monitor.Content);
        Assert.AreEqual(grpc.IsPrimary, monitor.IsPrimary);
        Assert.AreEqual(grpc.ThumbnailPath, monitor.ThumbnailPath);
        Assert.AreEqual(grpc.Bounds.X, monitor.Bounds.X);
        Assert.AreEqual(grpc.Bounds.Width, monitor.Bounds.Width);
        Assert.AreEqual(grpc.Bounds.Height, monitor.Bounds.Height);
        Assert.AreEqual(grpc.WorkingArea.Y, monitor.WorkingArea.Y);
        Assert.AreEqual(grpc.WorkingArea.Width, monitor.WorkingArea.Width);
        Assert.AreEqual(grpc.WorkingArea.Height, monitor.WorkingArea.Height);
    }

    [TestMethod]
    public void Monitor_MonitorDataToGrpc_PreservesBoundsFields() {
        var monitor = new Monitor {
            DeviceId = @"\\.\DISPLAY1",
            IsPrimary = true,
            Bounds = new System.Drawing.Rectangle(0, 0, 1920, 1080),
            WorkingArea = new System.Drawing.Rectangle(0, 40, 1920, 1040),
        };

        var grpc = DataAssist.MonitorDataToGrpc(monitor);

        Assert.AreEqual(monitor.Bounds.X, grpc.Bounds.X);
        Assert.AreEqual(monitor.Bounds.Y, grpc.Bounds.Y);
        Assert.AreEqual(monitor.Bounds.Width, grpc.Bounds.Width);
        Assert.AreEqual(monitor.Bounds.Height, grpc.Bounds.Height);
    }

    [TestMethod]
    public void Monitor_MonitorDataToGrpc_PreservesWorkingAreaFields() {
        var monitor = new Monitor {
            // Bounds 和 WorkingArea 故意使用不同的 Y / Height，确保能区分
            Bounds = new System.Drawing.Rectangle(0, 0, 1920, 1080),
            WorkingArea = new System.Drawing.Rectangle(0, 40, 1920, 1040),
        };

        var grpc = DataAssist.MonitorDataToGrpc(monitor);

        Assert.AreEqual(monitor.WorkingArea.X, grpc.WorkingArea.X, "WorkingArea.X");
        Assert.AreEqual(monitor.WorkingArea.Y, grpc.WorkingArea.Y, "WorkingArea.Y");
        Assert.AreEqual(monitor.WorkingArea.Width, grpc.WorkingArea.Width, "WorkingArea.Width");
        Assert.AreEqual(monitor.WorkingArea.Height, grpc.WorkingArea.Height, "WorkingArea.Height");
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5. Settings  ↔  Grpc_SettingsData
    // ════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Settings_Roundtrip_PreservesStringFields() {
        var src = MakeSettings();

        var grpc = DataAssist.SettingsToGrpc(src);
        var restored = DataAssist.GrpcToSettings(grpc);

        Assert.AreEqual(src.AppName, restored.AppName);
        Assert.AreEqual(src.AppVersion, restored.AppVersion);
        Assert.AreEqual(src.Language, restored.Language);
        Assert.AreEqual(src.WallpaperDir, restored.WallpaperDir);
    }

    [TestMethod]
    public void Settings_Roundtrip_PreservesBoolFields() {
        var src = MakeSettings();

        var grpc = DataAssist.SettingsToGrpc(src);
        var restored = DataAssist.GrpcToSettings(grpc);

        Assert.AreEqual(src.IsUpdated, restored.IsUpdated);
        Assert.AreEqual(src.IsAutoStart, restored.IsAutoStart);
        Assert.AreEqual(src.IsFirstRun, restored.IsFirstRun);
        Assert.AreEqual(src.IsAudioOnlyOnDesktop, restored.IsAudioOnlyOnDesktop);
        Assert.AreEqual(src.MouseInputMovAlways, restored.MouseInputMovAlways);
        Assert.AreEqual(src.IsScreenSaverOn, restored.IsScreenSaverOn);
        Assert.AreEqual(src.IsRunningLock, restored.IsRunningLock);
    }

    [TestMethod]
    public void Settings_Roundtrip_PreservesNumericFields() {
        var src = MakeSettings();

        var grpc = DataAssist.SettingsToGrpc(src);
        var restored = DataAssist.GrpcToSettings(grpc);

        Assert.AreEqual(src.WallpaperWaitTime, restored.WallpaperWaitTime);
        Assert.AreEqual(src.ProcessTimerInterval, restored.ProcessTimerInterval);
        Assert.AreEqual(src.WaitingTime, restored.WaitingTime);
    }

    [TestMethod]
    public void Settings_Roundtrip_PreservesEnumFields() {
        var src = MakeSettings();

        var grpc = DataAssist.SettingsToGrpc(src);
        var restored = DataAssist.GrpcToSettings(grpc);

        Assert.AreEqual(src.AppFocus, restored.AppFocus);
        Assert.AreEqual(src.AppFullscreen, restored.AppFullscreen);
        Assert.AreEqual(src.ApplicationTheme, restored.ApplicationTheme);
        Assert.AreEqual(src.BatteryPoweredn, restored.BatteryPoweredn);
        Assert.AreEqual(src.PowerSaving, restored.PowerSaving);
        Assert.AreEqual(src.RemoteDesktop, restored.RemoteDesktop);
        Assert.AreEqual(src.SystemBackdrop, restored.SystemBackdrop);
        Assert.AreEqual(src.StatuMechanism, restored.StatuMechanism);
        Assert.AreEqual(src.WallpaperScaling, restored.WallpaperScaling);
        Assert.AreEqual(src.InputForward, restored.InputForward);
        Assert.AreEqual(src.WallpaperArrangement, restored.WallpaperArrangement);
        Assert.AreEqual(src.ScreenSaverEffect, restored.ScreenSaverEffect);
    }

    [TestMethod]
    public void Settings_Roundtrip_PreservesSelectedMonitorCoreFields() {
        var src = MakeSettings();

        var grpc = DataAssist.SettingsToGrpc(src);
        var restored = DataAssist.GrpcToSettings(grpc);

        Assert.AreEqual(src.SelectedMonitor.DeviceId, restored.SelectedMonitor.DeviceId);
        Assert.AreEqual(src.SelectedMonitor.IsPrimary, restored.SelectedMonitor.IsPrimary);
        Assert.AreEqual(src.SelectedMonitor.Bounds.X, restored.SelectedMonitor.Bounds.X);
        Assert.AreEqual(src.SelectedMonitor.Bounds.Width, restored.SelectedMonitor.Bounds.Width);
        Assert.AreEqual(src.SelectedMonitor.Bounds.Height, restored.SelectedMonitor.Bounds.Height);
        Assert.AreEqual(src.SelectedMonitor.WorkingArea.Y, restored.SelectedMonitor.WorkingArea.Y);
        Assert.AreEqual(src.SelectedMonitor.WorkingArea.Height, restored.SelectedMonitor.WorkingArea.Height);
    }

    [TestMethod]
    public void Settings_Roundtrip_PreservesSelectedMonitorContent() {
        var src = MakeSettings();
        src.SelectedMonitor = new Monitor { DeviceId = "d1", Content = "Primary Display" };

        var grpc = DataAssist.SettingsToGrpc(src);
        var restored = DataAssist.GrpcToSettings(grpc);

        Assert.AreEqual("Primary Display", restored.SelectedMonitor.Content);
    }

    [TestMethod]
    public void Settings_Roundtrip_PreservesWhiteListScr() {
        var src = MakeSettings();
        src.WhiteListScr = [
            new ProcInfo { ProcName = "chrome.exe",  IconPath = @"C:\icon.ico", IsRunning = true  },
            new ProcInfo { ProcName = "notepad.exe", IconPath = string.Empty,   IsRunning = false },
        ];

        var grpc = DataAssist.SettingsToGrpc(src);
        var restored = DataAssist.GrpcToSettings(grpc);

        Assert.HasCount(2, restored.WhiteListScr);
        Assert.AreEqual("chrome.exe", restored.WhiteListScr[0].ProcName);
        Assert.AreEqual(@"C:\icon.ico", restored.WhiteListScr[0].IconPath);
        Assert.IsTrue(restored.WhiteListScr[0].IsRunning);
        Assert.AreEqual("notepad.exe", restored.WhiteListScr[1].ProcName);
        Assert.IsFalse(restored.WhiteListScr[1].IsRunning);
    }

    [TestMethod]
    public void Settings_Roundtrip_EmptyWhiteListScr_DoesNotThrow() {
        var src = MakeSettings();
        src.WhiteListScr = [];

        var grpc = DataAssist.SettingsToGrpc(src);
        var restored = DataAssist.GrpcToSettings(grpc);

        Assert.IsEmpty(restored.WhiteListScr);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 6. FromRuntimeDataGetPlayerData（mutation 方法）
    // ════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void FromRuntimeDataGetPlayerData_CopiesAllEffectPaths() {
        var runtime = new WpRuntimeData {
            WpEffectFilePathTemplate = @"C:\effects\template.json",
            WpEffectFilePathTemporary = @"C:\effects\temp.json",
            WpEffectFilePathUsing = @"C:\effects\using.json",
            DepthFilePath = @"C:\depth.png",
            AppInfo = new ApplicationInfo(),
        };
        var player = new WpPlayerData();

        DataAssist.FromRuntimeDataGetPlayerData(player, runtime);

        Assert.AreEqual(runtime.WpEffectFilePathTemplate, player.WpEffectFilePathTemplate);
        Assert.AreEqual(runtime.WpEffectFilePathTemporary, player.WpEffectFilePathTemporary);
        Assert.AreEqual(runtime.WpEffectFilePathUsing, player.WpEffectFilePathUsing);
        Assert.AreEqual(runtime.DepthFilePath, player.DepthFilePath);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 7. GrpcToPlayerData — effect path 字段（最近新增字段的回归测试）
    // ════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("GrpcToPlayerData must preserve WpEffectFilePathUsing, Template, Temporary — these are the fields central to the recent isFromPreview fix")]
    public void PlayerData_GrpcToPlayerData_PreservesEffectPathFields() {
        var grpc = new Grpc_WpPlayerData {
            WallpaperUid = "uid-effect-test",
            RType = Grpc_RuntimeType.RImage,
            FilePath = @"C:\wp\file.jpg",
            FolderPath = @"C:\wp",
            ThumbnailPath = @"C:\wp\thumb.jpg",
            WpEffectFilePathUsing    = @"C:\wp\effects\using.json",
            WpEffectFilePathTemplate = @"C:\wp\effects\template.json",
            WpEffectFilePathTemporary = @"C:\wp\effects\temp.json",
        };

        var player = DataAssist.GrpcToPlayerData(grpc);

        Assert.AreEqual(grpc.WpEffectFilePathUsing, player.WpEffectFilePathUsing,
            "WpEffectFilePathUsing must survive the gRPC round-trip");
        Assert.AreEqual(grpc.WpEffectFilePathTemplate, player.WpEffectFilePathTemplate,
            "WpEffectFilePathTemplate must survive the gRPC round-trip");
        Assert.AreEqual(grpc.WpEffectFilePathTemporary, player.WpEffectFilePathTemporary,
            "WpEffectFilePathTemporary must survive the gRPC round-trip");
    }

    [TestMethod]
    [Description("GrpcToPlayerData when all effect paths are empty strings should not throw and should preserve empty values")]
    public void PlayerData_GrpcToPlayerData_WhenEffectPathsEmpty_PreservesEmpty() {
        var grpc = new Grpc_WpPlayerData {
            WallpaperUid = "uid-no-effect",
            RType = Grpc_RuntimeType.RImage,
            FilePath = @"C:\wp\file.jpg",
            FolderPath = @"C:\wp",
            ThumbnailPath = string.Empty,
            WpEffectFilePathUsing     = string.Empty,
            WpEffectFilePathTemplate  = string.Empty,
            WpEffectFilePathTemporary = string.Empty,
        };

        var player = DataAssist.GrpcToPlayerData(grpc);

        Assert.AreEqual(string.Empty, player.WpEffectFilePathUsing);
        Assert.AreEqual(string.Empty, player.WpEffectFilePathTemplate);
        Assert.AreEqual(string.Empty, player.WpEffectFilePathTemporary);
    }

    [TestMethod]
    [Description("RuntimeData roundtrip: three effect paths must survive BasicData→Grpc→PlayerData chain")]
    public void RuntimeData_EffectPaths_SurviveRoundtripToPlayerData() {
        var runtime = new WpRuntimeData {
            WpEffectFilePathTemplate  = @"C:\effects\template.json",
            WpEffectFilePathTemporary = @"C:\effects\temp.json",
            WpEffectFilePathUsing     = @"C:\effects\using.json",
            DepthFilePath = @"C:\depth.png",
            AppInfo = new ApplicationInfo(),
        };

        var grpc     = DataAssist.RuntimeDataToGrpcData(runtime);
        var restored = DataAssist.GrpcToRuntimeData(grpc);

        Assert.AreEqual(runtime.WpEffectFilePathUsing,     restored.WpEffectFilePathUsing,
            "WpEffectFilePathUsing lost in RuntimeData roundtrip");
        Assert.AreEqual(runtime.WpEffectFilePathTemplate,  restored.WpEffectFilePathTemplate,
            "WpEffectFilePathTemplate lost in RuntimeData roundtrip");
        Assert.AreEqual(runtime.WpEffectFilePathTemporary, restored.WpEffectFilePathTemporary,
            "WpEffectFilePathTemporary lost in RuntimeData roundtrip");
    }

    // ════════════════════════════════════════════════════════════════════════
    // 辅助工厂方法
    // ════════════════════════════════════════════════════════════════════════

    private static WpBasicData MakeBasicData(FileType ftype = FileType.FImage) => new() {
        WallpaperUid = "uid-test-001",
        Title = "Test Wallpaper",
        Desc = "A test wallpaper description",
        Authors = "Test Author",
        PublishDate = "2024-01-01",
        Rating = 4,
        FType = ftype,
        IsSingleRType = true,
        Partition = "nature",
        Tags = "tag1,tag2",
        FolderName = "uid-test-001",
        FolderPath = @"C:\wallpapers\uid-test-001",
        FilePath = @"C:\wallpapers\uid-test-001\file.jpg",
        ThumbnailPath = @"C:\wallpapers\uid-test-001\thumb.jpg",
        Resolution = "1920x1080",
        AspectRatio = "16:9",
        FileSize = 2048576.ToString(),
        FileExtension = ".jpg",
        AppInfo = new ApplicationInfo {
            AppName = "VirtualPaper",
            AppVersion = "1.0.0",
            FileVersion = "1.0.0.1",
        },
    };

    private static Settings MakeSettings() => new() {
        AppName = "VirtualPaper",
        AppVersion = "1.0.0",
        Language = "zh-CN",
        WallpaperDir = @"C:\Wallpapers",
        IsUpdated = true,
        IsAutoStart = false,
        IsFirstRun = false,
        IsAudioOnlyOnDesktop = true,
        MouseInputMovAlways = false,
        IsScreenSaverOn = false,
        IsRunningLock = true,
        WallpaperWaitTime = 3000,
        ProcessTimerInterval = 5000,
        WaitingTime = 600,
        AppFocus = AppWpRunRulesEnum.Pause,
        AppFullscreen = AppWpRunRulesEnum.Pause,
        BatteryPoweredn = AppWpRunRulesEnum.Pause,
        PowerSaving = AppWpRunRulesEnum.Pause,
        RemoteDesktop = AppWpRunRulesEnum.Pause,
        ApplicationTheme = AppTheme.Dark,
        SystemBackdrop = AppSystemBackdrop.Mica,
        StatuMechanism = StatuMechanismEnum.Per,
        WallpaperScaling = WallpaperScaler.fill,
        InputForward = InputForwardMode.off,
        WallpaperArrangement = WallpaperArrangement.Per,
        ScreenSaverEffect = ScrEffect.Bubble,
        SelectedMonitor = new Monitor {
            DeviceId = @"\\.\DISPLAY1",
            IsPrimary = true,
            Content = "Primary",
            Bounds = new System.Drawing.Rectangle(0, 0, 1920, 1080),
            WorkingArea = new System.Drawing.Rectangle(0, 40, 1920, 1040),
        },
        WhiteListScr = [],
    };
}
