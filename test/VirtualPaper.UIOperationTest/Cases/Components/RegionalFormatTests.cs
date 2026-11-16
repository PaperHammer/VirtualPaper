using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class RegionalFormatTests {
    internal const string CaseName = "Localization.RegionalFormats.EffectsSaveAndReopen";

    public static async Task RunIsolatedAsync(UiTestContext context) {
        // Regional cultures and XAML/localizer caches are process-wide. Keep these
        // mutations separate from already-created controls in the regular host.
        string output = Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "regional-host");
        Directory.CreateDirectory(output);
        string report = Path.Combine(output, "results.json");
        var info = new ProcessStartInfo(Environment.ProcessPath!) {
            UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = AppContext.BaseDirectory, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "--report", report, "--module", CaseName }) info.ArgumentList.Add(argument);
        info.Environment[TestProcessEnvironment.SessionVariable] = Guid.NewGuid().ToString("N");
        info.Environment[TestProcessEnvironment.DataRootVariable] = Path.Combine(context.Root, "regional-data");
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Regional test host did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
            await File.WriteAllTextAsync(Path.Combine(output, "stdout.log"), await stdout);
            await File.WriteAllTextAsync(Path.Combine(output, "stderr.log"), await stderr);
            Check(process.ExitCode == 0 && File.Exists(report), $"Regional test host failed (exit {process.ExitCode})");
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(report));
            var root = json.RootElement;
            Check(root.GetProperty("Passed").GetBoolean() && root.GetProperty("ExpectedCount").GetInt32() == 1,
                "The isolated regional case was missing, skipped or failed");
            var results = root.GetProperty("Results");
            Check(results.GetArrayLength() == 1 && results[0].GetProperty("Name").GetString() == CaseName
                && results[0].GetProperty("Passed").GetBoolean(), "The isolated host did not pass the requested case");
            foreach (var property in results[0].GetProperty("Evidence").EnumerateObject())
                context.Evidence[property.Name] = property.Value.Clone();
            context.Evidence["RegionalHostProcessId"] = process.Id;
            context.Evidence["RegionalHostReport"] = report;
        }
        finally {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    public static async Task RunAsync(UiTestContext context) {
        var original = CultureInfo.CurrentCulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentCulture;
        var evidence = new List<object>();
        try {
            foreach (string region in new[] { "zh-CN", "en-US", "de-DE", "fr-FR" }) {
                // Include user overrides: preserving just the culture name is insufficient.
                var culture = (CultureInfo)new CultureInfo(region).Clone();
                culture.DateTimeFormat.ShortDatePattern = "yyyy/MM/dd";
                culture.NumberFormat.NumberDecimalDigits = 3;
                CultureInfo.CurrentCulture = CultureInfo.DefaultThreadCurrentCulture = culture;
                foreach (string language in new[] { "en-US", "de-DE", "ja-JP", "zh-CN" }) {
                    await LanguageUtil.SetLanguageAsync(language);
                    Check(ReferenceEquals(CultureInfo.CurrentCulture, culture)
                        && CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern == "yyyy/MM/dd"
                        && CultureInfo.CurrentCulture.NumberFormat.NumberDecimalDigits == 3,
                        "UI language replaced the regional culture or its user overrides");
                }
                var properties = JsonSerializer.SerializeToNode(new Picture3DCostumize(),
                    Picture3DCostumizeContext.Default.Picture3DCostumize)!.AsObject();
                string name = nameof(UniverseCostumise.Saturation);
                properties[name]!["Step"] = .01;
                var args = new StartArgsWeb {
                    RuntimeType = RuntimeType.RImage.ToString(),
                    WpEffectFilePathUsing = Path.Combine(context.Root, region + "-using.json"),
                    WpEffectFilePathTemplate = Path.Combine(context.Root, region + "-template.json"),
                    WpEffectFilePathTemporary = Path.Combine(context.Root, region + "-temporary.json")
                };
                foreach (string path in new[] { args.WpEffectFilePathUsing, args.WpEffectFilePathTemplate, args.WpEffectFilePathTemporary })
                    JsonNodeUtil.Write(path, properties);
                var apply = new RecordingApply();
                var payload = new FrameworkPayload {
                    [NaviPayloadKey.StartArgs] = args,
                    [NaviPayloadKey.IEffectService] = new RecordingEffects(),
                    [NaviPayloadKey.ApplyService] = apply
                };
                var page = await context.OpenAsync<GeneralEffect>(payload);
                var slider = ((StackPanel)page.FindName("skPanel")).Children.OfType<Slider>().Single(item => item.Name == name);
                Check(slider.StepFrequency == .01, $"JSON decimal step changed in {region}: {slider.StepFrequency}");
                Pattern<IRangeValueProvider>(slider, PatternInterface.RangeValue).SetValue(1.13);
                // The production label uses the regional decimal separator, independent of UI language.
                var grid = ((StackPanel)page.FindName("skPanel")).Children.OfType<Grid>()
                    .Single(item => item.Children.OfType<TextBlock>().Any(text => text.Name == name + "_Value"));
                string label = grid.Children.OfType<TextBlock>().Single(item => item.Name == name + "_Value").Text;
                Check(label == 1.13.ToString("0.#", culture), "Effect value label ignored regional formatting");
                Invoke((Button)page.FindName("btnSaveAndApply"));
                await context.WaitAsync(() => apply.Args != null);
                using var json = JsonDocument.Parse(await File.ReadAllTextAsync(args.WpEffectFilePathUsing));
                Check(json.RootElement.GetProperty(name).GetProperty("Value").GetDouble() == 1.13,
                    "Regional decimal formatting corrupted persisted effect JSON");
                var reopened = await context.OpenAsync<GeneralEffect>(payload);
                var restored = ((StackPanel)reopened.FindName("skPanel")).Children.OfType<Slider>().Single(item => item.Name == name);
                Check(restored.Value == 1.13 && restored.StepFrequency == .01, "Reopened effect changed its numeric value or step");
                evidence.Add(new { Region = region, UiLanguage = LanguageUtil.CurrentLanguage, DecimalLabel = label,
                    PersistedValue = 1.13, Step = restored.StepFrequency, UserOverridesPreserved = true });
            }
            context.Evidence["RegionalEffectRoundTrips"] = evidence;
        }
        finally {
            CultureInfo.CurrentCulture = original;
            CultureInfo.DefaultThreadCurrentCulture = originalDefault;
            await LanguageUtil.SetLanguageAsync("en-US");
        }
    }

    private sealed class RecordingApply : IApplyService {
        public ApplyEventArgs? Args { get; private set; }
        public void OnApply(ApplyEventArgs args) => Args = args;
    }
    private sealed class RecordingEffects : IEffectService {
        public void UpdateEffectValue<T>(EffectValueChanged<T> value) { }
    }
}
