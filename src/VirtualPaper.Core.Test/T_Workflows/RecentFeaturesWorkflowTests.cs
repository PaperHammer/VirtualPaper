using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.ML.SuperResolution.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.WpSettingsPanel.ViewModels;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
[DoNotParallelize]
[TestCategory("Workflow")]
public class RecentFeaturesWorkflowTests {
    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(0, true)]
    [DataRow(1, true)]
    public async Task ScreenFit_ImportApplyAndReload_PreservesSourceAndSelectedFit(int fit, bool enhance) {
        using var environment = new WallpaperWorkflowEnvironment();
        var model = new TestUpscaler();
        var viewModel = CreateFit(environment, model);
        viewModel.FitIndex = fit;
        viewModel.UseEnhancement = enhance;
        IWpBasicData? imported = null;
        viewModel.Imported += data => imported = data;
        Assert.IsTrue(await viewModel.ApplyAsync(CancellationToken.None), viewModel.ErrorMessage);
        Assert.IsNotNull(imported);
        Assert.AreEqual(enhance ? 1 : 0, model.Runs);
        Assert.AreEqual(enhance, model.Disposed);
        using (var image = new Bitmap(imported.FilePath)) {
            Assert.AreEqual(enhance ? viewModel.Plan!.OutputWidth : 48, image.Width);
            Assert.AreEqual(enhance ? viewModel.Plan!.OutputHeight : 64, image.Height);
        }
        var args = await RunningArgs(environment);
        var properties = JsonNodeUtil.GetWritableJson(args.WpEffectFilePathUsing!);
        Assert.AreEqual(fit == 0 ? (int)ObjectFit.Contain : (int)ObjectFit.Cover, properties["Scaling"]!["Value"]!.GetValue<int>());
        Assert.AreEqual(0, properties["Atmosphere"]!["Value"]!.GetValue<int>());
        await environment.SettingsClient.LoadAsync<List<IWallpaperLayout>>();
        Assert.AreEqual(imported.FolderPath, environment.SettingsClient.WallpaperLayouts.Single().FolderPath);
        var reopened = await environment.OpenLibraryAsync();
        Assert.AreEqual(imported.WallpaperUid, reopened.Index.Query(0, 10).Single().Uid);
        Assert.IsFalse(viewModel.IsBusy);
        Assert.AreEqual(string.Empty, viewModel.ProgressText);
        AssertSourceAndTempClean(environment);
    }

    [TestMethod]
    [DataRow("cancel")]
    [DataRow("model-failure")]
    [DataRow("display-removed")]
    public async Task ScreenFit_InterruptedEnhancement_DoesNotImportOrApply(string failure) {
        using var environment = new WallpaperWorkflowEnvironment();
        using var cancellation = new CancellationTokenSource();
        var model = new TestUpscaler {
            BeforeRun = ct => {
                if (failure == "cancel") { cancellation.Cancel(); ct.ThrowIfCancellationRequested(); }
                if (failure == "model-failure") throw new InvalidOperationException("Test inference failure");
                if (failure == "display-removed") environment.Environment.Monitors.Object.Monitors.Clear();
            }
        };
        var viewModel = CreateFit(environment, model);
        viewModel.UseEnhancement = true;
        bool imported = false;
        viewModel.Imported += _ => imported = true;
        Assert.IsFalse(await viewModel.ApplyAsync(cancellation.Token));
        Assert.IsFalse(imported);
        Assert.IsTrue(model.Disposed);
        Assert.IsEmpty(environment.Control.Wallpapers);
        Assert.IsEmpty(Directory.GetDirectories(environment.SettingsClient.Settings.WallpaperDir));
        Assert.IsFalse(viewModel.IsBusy);
        Assert.AreEqual(string.Empty, viewModel.ProgressText);
        Assert.AreEqual(failure switch {
            "cancel" => "ScreenFit_Canceled",
            "model-failure" => "ScreenFit_EnhanceFailed",
            _ => "ScreenFit_DisplayChanged"
        }, viewModel.ErrorMessage);
        AssertSourceAndTempClean(environment);
    }

    [TestMethod]
    [DataRow(RuntimeType.RImage)]
    [DataRow(RuntimeType.RImage3D)]
    public async Task Effects_SaveApplyCloseAndRecreatePlayer_PreservesCustomization(RuntimeType runtime) {
        using var environment = new WallpaperWorkflowEnvironment();
        var data = await environment.ImportAsync();
        string? depthPath = null;
        if (runtime == RuntimeType.RImage3D) {
            Directory.CreateDirectory(Constants.CommonPaths.TempDir);
            depthPath = Path.Combine(Constants.CommonPaths.TempDir, "workflow-depth.png");
            File.Copy(environment.SourcePath, depthPath);
        }
        var previewJson = await environment.Client.GetPlayerStartArgsAsync(data, runtime, depthPath, CancellationToken.None);
        var preview = JsonSerializer.Deserialize(previewJson, StartArgsWebContext.Default.StartArgsWeb)!;
        var properties = JsonNodeUtil.GetWritableJson(preview.WpEffectFilePathUsing!).AsObject();
        properties["Atmosphere"]!["Value"] = (int)AtmosphereEffect.Snow;
        properties["AtmosphereStyle"]!["Value"] = 0;
        properties["AtmosphereIntensity"]!["Value"] = 71;
        if (runtime == RuntimeType.RImage3D) {
            properties["Parallax"]!["Value"] = true;
            properties["ParallaxStrength"]!["Value"] = 2;
            properties["ParallaxAutoDrift"]!["Value"] = true;
        }
        // Repeated schema refresh must keep values saved by the effect editor.
        for (int i = 0; i < 3; i++) {
            AtmosphereProperties.EnsureAtmosphereProperties(properties);
            if (runtime == RuntimeType.RImage3D) ParallaxProperties.EnsureDepthProperties(properties);
        }
        JsonNodeUtil.Write(preview.WpEffectFilePathUsing!, properties);
        Assert.IsTrue((await environment.Client.SetWallpaperAsync(environment.Monitor, data, runtime, true,
            depthPath, preview.WpEffectFilePathUsing, preview.WpEffectFilePathTemplate,
            preview.WpEffectFilePathTemporary, CancellationToken.None)).IsFinished);
        var firstRun = await RunningArgs(environment);
        AssertEffects(firstRun, runtime);
        Assert.StartsWith(environment.SettingsClient.Settings.WallpaperDir, firstRun.WpEffectFilePathUsing!);
        await environment.Client.CloseAllWallpapersAsync();
        Directory.Delete(Constants.CommonPaths.TempDir, recursive: true);

        // Recreate using persisted per-display runtime paths, as restoration does.
        var metadata = await environment.Client.GetWallpaperAsync(data.FolderPath, environment.Monitor.Content, runtime.ToString());
        Assert.AreEqual(firstRun.WpEffectFilePathUsing, metadata.WpRuntimeData.WpEffectFilePathUsing);
        Assert.IsTrue((await environment.Client.SetWallpaperAsync(environment.Monitor, data, runtime, false,
            metadata.WpRuntimeData.DepthFilePath, metadata.WpRuntimeData.WpEffectFilePathUsing,
            metadata.WpRuntimeData.WpEffectFilePathTemplate, metadata.WpRuntimeData.WpEffectFilePathTemporary,
            CancellationToken.None)).IsFinished);
        var secondRun = await RunningArgs(environment);
        Assert.AreEqual(firstRun.WpEffectFilePathUsing, secondRun.WpEffectFilePathUsing);
        AssertEffects(secondRun, runtime);
        Assert.HasCount(2, environment.Players);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MissingOrLockedPreviewConfiguration_DoesNotReportAppliedWallpaper(bool locked) {
        using var environment = new WallpaperWorkflowEnvironment();
        var data = await environment.ImportAsync();
        var json = await environment.Client.GetPlayerStartArgsAsync(data, RuntimeType.RImage, null, CancellationToken.None);
        var preview = JsonSerializer.Deserialize(json, StartArgsWebContext.Default.StartArgsWeb)!;
        using var lockedFile = locked ? new FileStream(preview.WpEffectFilePathUsing!, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        if (!locked) File.Delete(preview.WpEffectFilePathUsing!);
        var result = await environment.Client.SetWallpaperAsync(environment.Monitor, data, RuntimeType.RImage, true,
            null, preview.WpEffectFilePathUsing, preview.WpEffectFilePathTemplate, preview.WpEffectFilePathTemporary,
            CancellationToken.None);
        Assert.IsFalse(result.IsFinished);
        Assert.IsEmpty(environment.Control.Wallpapers);
    }

    private static ScreenFitViewModel CreateFit(WallpaperWorkflowEnvironment environment, ISuperResolution model) {
        var viewModel = new ScreenFitViewModel(environment.MonitorClient.Object, environment.Client,
            environment.SettingsClient, Mock.Of<IStoragePicker>(), () => model);
        viewModel.SetSource(environment.SourcePath, 48, 64);
        return viewModel;
    }

    private static async Task<StartArgsWeb> RunningArgs(WallpaperWorkflowEnvironment environment) {
        var json = await environment.Client.GetPlayerStartArgsByMonitorIdAsync(environment.Monitor.DeviceId, CancellationToken.None);
        return JsonSerializer.Deserialize(json, StartArgsWebContext.Default.StartArgsWeb)!;
    }

    private static void AssertEffects(StartArgsWeb args, RuntimeType runtime) {
        var properties = JsonNodeUtil.GetWritableJson(args.WpEffectFilePathUsing!);
        Assert.AreEqual((int)AtmosphereEffect.Snow, properties["Atmosphere"]!["Value"]!.GetValue<int>());
        Assert.AreEqual(71, properties["AtmosphereIntensity"]!["Value"]!.GetValue<int>());
        Assert.AreEqual(runtime.ToString(), args.RuntimeType);
        if (runtime == RuntimeType.RImage3D) {
            Assert.IsTrue(properties["Parallax"]!["Value"]!.GetValue<bool>());
            Assert.AreEqual(2, properties["ParallaxStrength"]!["Value"]!.GetValue<int>());
            Assert.IsTrue(properties["ParallaxAutoDrift"]!["Value"]!.GetValue<bool>());
            Assert.IsTrue(File.Exists(args.DepthFilePath));
        }
    }

    private static void AssertSourceAndTempClean(WallpaperWorkflowEnvironment environment) {
        CollectionAssert.AreEqual(environment.SourceHash, SHA256.HashData(File.ReadAllBytes(environment.SourcePath)));
        Assert.IsEmpty(Directory.Exists(Constants.CommonPaths.TempDir)
            ? Directory.GetDirectories(Constants.CommonPaths.TempDir, "screen-fit-*") : []);
    }

    // Deterministic model boundary: real image output, no GPU/model dependency in workflow CI.
    // Real inference remains covered by the existing ML Integration category.
    private sealed class TestUpscaler : ISuperResolution {
        public string ModelPath => "workflow-test-model";
        public int Runs { get; private set; }
        public bool Disposed { get; private set; }
        public Action<CancellationToken>? BeforeRun { get; init; }
        public void LoadModel(string? path = null) { }
        public string RunAndSave(string input, string output, uint width, uint height, CancellationToken ct = default) {
            Runs++;
            BeforeRun?.Invoke(ct);
            ct.ThrowIfCancellationRequested();
            using var source = new Bitmap(input);
            using var resized = new Bitmap(source, checked((int)width), checked((int)height));
            resized.Save(output, ImageFormat.Png);
            return output;
        }
        public void Dispose() => Disposed = true;
    }
}
