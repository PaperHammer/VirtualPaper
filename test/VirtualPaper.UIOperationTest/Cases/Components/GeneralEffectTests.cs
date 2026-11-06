using System.Text.Json;
using System.Text.Json.Nodes;
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
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class GeneralEffectTests {
    public static async Task EditRestoreSaveAndReopenAsync(UiTestContext context, RuntimeType runtime) {
        var properties = JsonSerializer.SerializeToNode(new Picture3DCostumize(), Picture3DCostumizeContext.Default.Picture3DCostumize)!.AsObject();
        if (runtime == RuntimeType.RImage) {
            properties.Remove(nameof(Picture3DCostumize.Parallax));
            properties.Remove(nameof(Picture3DCostumize.ParallaxStrength));
            properties.Remove(nameof(Picture3DCostumize.ParallaxAutoDrift));
        }
        AtmosphereProperties.EnsureAtmosphereProperties(properties);
        properties["Atmosphere"]!["Value"] = (int)AtmosphereEffect.Snow;
        properties["AtmosphereIntensity"]!["Value"] = 50;
        var args = new StartArgsWeb {
            RuntimeType = runtime.ToString(),
            WpEffectFilePathUsing = Path.Combine(context.Root, "using.json"),
            WpEffectFilePathTemplate = Path.Combine(context.Root, "template.json"),
            WpEffectFilePathTemporary = Path.Combine(context.Root, "temporary.json")
        };
        foreach (var path in new[] { args.WpEffectFilePathUsing, args.WpEffectFilePathTemplate, args.WpEffectFilePathTemporary })
            JsonNodeUtil.Write(path, properties);
        var effects = new RecordingEffects();
        var apply = new RecordingApply();
        var payload = new FrameworkPayload {
            [NaviPayloadKey.StartArgs] = args,
            [NaviPayloadKey.IEffectService] = effects,
            [NaviPayloadKey.ApplyService] = apply
        };
        var page = await context.OpenAsync<GeneralEffect>(payload);
        var panel = (StackPanel)page.FindName("skPanel");
        var groups = panel.Children.OfType<DropDownButton>().ToArray();
        Check(groups.Length == (runtime == RuntimeType.RImage3D ? 2 : 1), "Unexpected effect groups");
        var groupCounts = groups.Select(GroupCount).ToArray();
        int rootCount = panel.Children.Count;
        var atmosphere = groups.Single(group => group.Name == "AtmosphereOptions");

        for (int i = 0; i < 5; i++) {
            var slider = await OpenIntensityAsync(context, atmosphere);
            Pattern<IRangeValueProvider>(slider, PatternInterface.RangeValue).SetValue(75);
            await context.WaitAsync(() => effects.Intensity == 75);
            atmosphere.Flyout.Hide();
            await context.WaitAsync(() => !slider.IsLoaded);
            Invoke((Button)page.FindName("btnRestore"));
            await context.WaitAsync(() => slider.Value == 50);
            Check(panel.Children.Count == rootCount, "Root controls accumulated after restore");
            Check(groups.Select(GroupCount).SequenceEqual(groupCounts), "Flyout controls accumulated after restore");
            Check(groups.All(group => panel.Children.Contains(group)), "A flyout detached after restore");
        }

        var edited = await OpenIntensityAsync(context, atmosphere);
        Pattern<IRangeValueProvider>(edited, PatternInterface.RangeValue).SetValue(71);
        await context.WaitAsync(() => effects.Intensity == 71);
        atmosphere.Flyout.Hide();
        await context.WaitAsync(() => !edited.IsLoaded);
        Invoke((Button)page.FindName("btnSaveAndApply"));
        await context.WaitAsync(() => apply.Args != null);
        Check(apply.Args!.WpEffectFilePathUsing == args.WpEffectFilePathUsing, "Apply used a different config path");
        Check(JsonNodeUtil.GetWritableJson(args.WpEffectFilePathUsing)["AtmosphereIntensity"]!["Value"]!.GetValue<double>() == 71,
            "Apply did not persist the edited intensity");

        // Recreate the production page and verify the visible control reads the saved config.
        var reopened = await context.OpenAsync<GeneralEffect>(payload);
        var reopenedPanel = (StackPanel)reopened.FindName("skPanel");
        Check(reopenedPanel.Children.Count == rootCount, "Reopened page has a different control count");
        var reopenedGroup = reopenedPanel.Children.OfType<DropDownButton>().Single(group => group.Name == "AtmosphereOptions");
        var restored = await OpenIntensityAsync(context, reopenedGroup);
        Check(restored.Value == 71, "Reopened UI did not load the saved intensity");
        reopenedGroup.Flyout.Hide();
        await context.WaitAsync(() => !restored.IsLoaded);
    }

    private static int GroupCount(DropDownButton group) => ((StackPanel)((Flyout)group.Flyout).Content).Children.Count;

    private static async Task<Slider> OpenIntensityAsync(UiTestContext context, DropDownButton group) {
        await context.WaitAsync(() => group.IsLoaded && group.ActualWidth > 0, "Load effect option button");
        group.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
        await context.WaitForLayoutAsync();
        Pattern<IExpandCollapseProvider>(group, PatternInterface.ExpandCollapse).Expand();
        var slider = ((StackPanel)((Flyout)group.Flyout).Content).Children.OfType<Slider>()
            .Single(control => control.Name == "AtmosphereIntensity");
        try {
            await context.WaitAsync(() => slider.IsLoaded && slider.ActualWidth > 0, "Open atmosphere options");
        }
        catch (TimeoutException ex) {
            context.Evidence["FlyoutState"] = new {
                GroupLoaded = group.IsLoaded, SliderLoaded = slider.IsLoaded,
                SliderWidth = slider.ActualWidth,
                ExpandState = Pattern<IExpandCollapseProvider>(group, PatternInterface.ExpandCollapse).ExpandCollapseState.ToString()
            };
            context.SaveFailureSnapshot("atmosphere-open-timeout");
            throw new InvalidOperationException($"Flyout state: groupLoaded={group.IsLoaded}, sliderLoaded={slider.IsLoaded}, " +
                $"sliderWidth={slider.ActualWidth}, expandState={Pattern<IExpandCollapseProvider>(group, PatternInterface.ExpandCollapse).ExpandCollapseState}", ex);
        }
        Check(slider.IsEnabled, "Intensity slider is disabled");
        return slider;
    }

    private sealed class RecordingEffects : IEffectService {
        public double? Intensity { get; private set; }
        public void UpdateEffectValue<T>(EffectValueChanged<T> value) {
            if (value.PropertyName == "AtmosphereIntensity") Intensity = Convert.ToDouble(value.Value);
        }
    }

    private sealed class RecordingApply : IApplyService {
        public ApplyEventArgs? Args { get; private set; }
        public void OnApply(ApplyEventArgs args) => Args = args;
    }
}
