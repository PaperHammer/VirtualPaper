using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Events;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.PlayerWeb.Core.Interfaces;
using VirtualPaper.PlayerWeb.Core.Utils.Interfaces;
using VirtualPaper.PlayerWeb.Core.WebView.Components.General;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.Sandbox.WinUI.Preview;

// An opt-in component workflow in the preview app, never loaded by the product UI.
// Uses real XAML, bindings/events and Invoke providers, without coordinate clicks.
internal static class GeneralEffectWorkflow {
    public static async Task RunAsync(string reportPath) {
        string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.EffectUi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Window? window = null;
        GeneralEffect? page = null;
        Exception? unhandled = null;
        int restoreChecks = 0;
        var report = new JsonObject();
        void OnUnhandled(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) {
            unhandled = e.Exception;
            e.Handled = true;
        }
        Application.Current.UnhandledException += OnUnhandled;
        try {
            await LanguageUtil.InitializeLocalizerForUnpackaged("en-US");
            var properties = JsonSerializer.SerializeToNode(new Picture3DCostumize(), Picture3DCostumizeContext.Default.Picture3DCostumize)!.AsObject();
            AtmosphereProperties.EnsureAtmosphereProperties(properties);
            ParallaxProperties.EnsureDepthProperties(properties);
            properties["Atmosphere"]!["Value"] = (int)AtmosphereEffect.Snow;
            properties["AtmosphereIntensity"]!["Value"] = 50;
            var args = new StartArgsWeb {
                RuntimeType = nameof(RuntimeType.RImage3D),
                WpEffectFilePathUsing = Path.Combine(root, "using.json"),
                WpEffectFilePathTemplate = Path.Combine(root, "template.json"),
                WpEffectFilePathTemporary = Path.Combine(root, "temporary.json")
            };
            foreach (var path in new[] { args.WpEffectFilePathUsing, args.WpEffectFilePathTemplate, args.WpEffectFilePathTemporary })
                JsonNodeUtil.Write(path, properties);

            var effects = new RecordingEffects();
            var apply = new RecordingApply();
            var frame = new Frame();
            window = new Window { Content = frame, Title = "VirtualPaper component workflow" };
            Check(frame.Navigate(typeof(GeneralEffect), new FrameworkPayload {
                [NaviPayloadKey.StartArgs] = args,
                [NaviPayloadKey.IEffectService] = effects,
                [NaviPayloadKey.ApplyService] = apply
            }), "Navigation failed");
            page = (GeneralEffect)frame.Content;
            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Loaded += (_, _) => loaded.TrySetResult(true);
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Yield();
            if (unhandled != null) throw new InvalidOperationException("Page load failed", unhandled);

            var panel = (StackPanel)page.FindName("skPanel");
            var groups = panel.Children.OfType<DropDownButton>().ToArray();
            Check(groups.Length == 2, "Atmosphere and parallax groups must both be present");
            var groupPanels = groups.Select(g => (StackPanel)((Flyout)g.Flyout).Content).ToArray();
            int rootCount = panel.Children.Count;
            int[] groupCounts = groupPanels.Select(p => p.Children.Count).ToArray();
            var intensity = groupPanels.SelectMany(p => p.Children).OfType<Slider>().Single(s => s.Name == "AtmosphereIntensity");
            var restore = (Button)page.FindName("btnRestore");
            for (int i = 0; i < 5; i++) {
                intensity.Value = 75;
                Invoke(restore);
                await WaitUntilAsync(() => intensity.Value == 50 || unhandled != null);
                if (unhandled != null) throw new InvalidOperationException("Restore failed", unhandled);
                Check(panel.Children.Count == rootCount, "Root controls accumulated on restore");
                Check(groupPanels.Select((p, index) => p.Children.Count == groupCounts[index]).All(same => same), "Flyout controls accumulated on restore");
                Check(groups.All(g => panel.Children.Contains(g)), "A flyout button detached on restore");
                restoreChecks++;
            }
            Check(effects.Changes > 0, "Slider changes did not reach IEffectService");
            intensity.Value = 71;
            Invoke((Button)page.FindName("btnSaveAndApply"));
            await WaitUntilAsync(() => apply.Args != null || unhandled != null);
            if (unhandled != null) throw new InvalidOperationException("Apply failed", unhandled);
            Check(apply.Args!.WpEffectFilePathUsing == args.WpEffectFilePathUsing, "Apply callback used a different config");
            Check(JsonNodeUtil.GetWritableJson(args.WpEffectFilePathUsing)["AtmosphereIntensity"]!["Value"]!.GetValue<double>() == 71,
                "Apply did not persist the edited intensity");
            report["passed"] = true;
            report["restoreCycles"] = restoreChecks;
            report["effectChanges"] = effects.Changes;
            report["applyPersisted"] = true;
            System.Environment.ExitCode = 0;
        }
        catch (Exception ex) {
            report["passed"] = false;
            report["restoreCycles"] = restoreChecks;
            report["error"] = ex.ToString();
            System.Environment.ExitCode = 1;
        }
        finally {
            Application.Current.UnhandledException -= OnUnhandled;
            page?.Dispose();
            window?.Close();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(reportPath, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Invoke(Button button) {
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(button) ?? new ButtonAutomationPeer(button);
        var invoke = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider
            ?? throw new InvalidOperationException("Button does not expose Invoke");
        invoke.Invoke();
    }

    private static async Task WaitUntilAsync(Func<bool> condition) {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition()) {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("Control operation did not complete");
            await Task.Delay(20);
        }
    }

    private static void Check(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class RecordingEffects : IEffectService {
        public int Changes { get; private set; }
        public void UpdateEffectValue<T>(EffectValueChanged<T> value) => Changes++;
    }

    private sealed class RecordingApply : IApplyService {
        public ApplyEventArgs? Args { get; private set; }
        public void OnApply(ApplyEventArgs args) => Args = args;
    }
}
