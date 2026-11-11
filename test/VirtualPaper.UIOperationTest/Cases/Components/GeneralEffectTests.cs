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
            // Restore rebuilds parameter controls; inspect the current control,
            // not the slider detached from the preceding flyout.
            await context.WaitAsync(() => ((StackPanel)((Flyout)atmosphere.Flyout).Content).Children
                .OfType<Slider>().Single(control => control.Name == "AtmosphereIntensity").Value == 50);
            Check(panel.Children.Count == rootCount, "Root controls accumulated after restore");
            Check(groups.Select(GroupCount).SequenceEqual(groupCounts), "Flyout controls accumulated after restore");
            Check(groups.All(group => panel.Children.Contains(group)), "A flyout detached after restore");
        }

        await VerifyPresetTrialAsync(context, page, effects, args);

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

    private static async Task VerifyPresetTrialAsync(UiTestContext context, GeneralEffect page, RecordingEffects effects, StartArgsWeb args) {
        var selector = (ComboBox)page.FindName("PresetSelector");
        await context.WaitAsync(() => selector.Items.OfType<ComboBoxItem>().Any(item => Equals(item.Tag, "builtin:Snow")),
            "Load effect preset choices");
        string before = page.GetPresetParameters().ToJsonString();
        string saved = await File.ReadAllTextAsync(args.WpEffectFilePathUsing);
        int edits = 0;
        EventHandler onEdited = (_, _) => edits++;
        page.ParametersChanged += onEdited;
        try {
            foreach (string id in new[] { "builtin:Soft", "builtin:Snow" }) {
                var presetItem = selector.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, id));
                var preset = (EffectPreset)presetItem.DataContext;
                selector.SelectedItem = presetItem;
                await context.WaitForLayoutAsync();
                Check(ReferenceEquals(selector.SelectedItem, presetItem), "Loading parameter controls reset the selected preset to Custom");
                Check(edits == 0, "Loading a preset emitted a manual parameter edit");
                Check(effects.Saturation == preset.Values[nameof(UniverseCostumise.Saturation)].GetDouble()
                    && effects.Intensity == preset.Values[nameof(UniverseCostumise.AtmosphereIntensity)].GetDouble(),
                    "Trying a preset did not send its values to the effect service");
                Check((double)page.GetPresetParameters()[nameof(UniverseCostumise.Saturation)]!["Value"]! == effects.Saturation,
                    "Displayed preset parameters differ from the values sent to the player");

                Pattern<IExpandCollapseProvider>(selector, PatternInterface.ExpandCollapse).Expand();
                await context.WaitForLayoutAsync();
                Check(ReferenceEquals(selector.SelectedItem, presetItem), "Reopening the preset dropdown replaced the active choice");
                Pattern<IExpandCollapseProvider>(selector, PatternInterface.ExpandCollapse).Collapse();
            }

            await VerifyPresetCatalogRefreshAsync(context, page, selector, effects, args);
            Check(edits == 0, "Refreshing and reapplying a saved preset emitted a manual parameter edit");

            var saturation = ((StackPanel)page.FindName("skPanel")).Children.OfType<Slider>()
                .Single(item => item.Name == nameof(UniverseCostumise.Saturation));
            Pattern<IRangeValueProvider>(saturation, PatternInterface.RangeValue).SetValue(1.5);
            await context.WaitAsync(() => effects.Saturation == 1.5 && edits == 1);
            Check((selector.SelectedItem as ComboBoxItem)?.Tag == null, "Manual editing did not switch the preset label to Custom");
            Invoke((Button)page.FindName("CancelPreset"));
            await context.WaitForLayoutAsync();
            Check(page.GetPresetParameters().ToJsonString() == before, "Cancel did not restore the parameters from before the first trial");
            Check(saved == await File.ReadAllTextAsync(args.WpEffectFilePathUsing), "Trying or cancelling presets changed the saved wallpaper config");
            Check(edits == 1, "Programmatic restoration emitted a manual parameter edit");
            context.Evidence["PresetSelectionTrialCancelVerified"] = true;
        }
        finally { page.ParametersChanged -= onEdited; }
    }

    private static async Task VerifyPresetCatalogRefreshAsync(UiTestContext context, GeneralEffect page,
        ComboBox selector, RecordingEffects effects, StartArgsWeb args) {
        Check(TestProcessEnvironment.IsEnabled && string.Equals(Constants.CommonPaths.AppDataDir,
            TestProcessEnvironment.DataRoot, StringComparison.OrdinalIgnoreCase), "Preset catalog tests require isolated app data");
        var store = new EffectPresetStore();
        var original = EffectPresets.Capture("UI preset " + Guid.NewGuid().ToString("N"),
            Enum.Parse<RuntimeType>(args.RuntimeType!), page.GetPresetParameters());
        original.Values[nameof(UniverseCostumise.Saturation)] = JsonSerializer.SerializeToElement(0.63);
        ComboBoxItem? FindItem() => selector.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, original.Id));
        async Task ReloadCatalogAsync(Func<bool> loaded) {
            var provider = Pattern<IExpandCollapseProvider>(selector, PatternInterface.ExpandCollapse);
            provider.Expand();
            try { await context.WaitAsync(loaded, "Refresh changed preset catalog"); }
            finally { provider.Collapse(); }
        }

        try {
            await store.SaveAsync(original);
            await ReloadCatalogAsync(() => FindItem() != null);
            var item = FindItem()!;
            selector.SelectedItem = item;
            await context.WaitAsync(() => effects.Saturation == 0.63);

            var renamed = original with { Name = "Renamed " + original.Name };
            await store.SaveAsync(renamed);
            await ReloadCatalogAsync(() => FindItem()?.DataContext is EffectPreset preset && preset.Name == renamed.Name);
            Check(ReferenceEquals(selector.SelectedItem, item), "Renaming a preset cleared its active selection");
            Check(effects.Saturation == 0.63, "Renaming a preset changed the live effect");

            var revised = renamed with { Values = new(renamed.Values) };
            revised.Values[nameof(UniverseCostumise.Saturation)] = JsonSerializer.SerializeToElement(1.13);
            await store.SaveAsync(revised);
            string beforeRefresh = page.GetPresetParameters().ToJsonString();
            await ReloadCatalogAsync(() => FindItem()?.DataContext is EffectPreset preset
                && preset.Values[nameof(UniverseCostumise.Saturation)].GetDouble() == 1.13
                && (selector.SelectedItem as ComboBoxItem)?.Tag == null);
            Check(effects.Saturation == 0.63 && page.GetPresetParameters().ToJsonString() == beforeRefresh,
                "Refreshing revised preset parameters changed the live trial before selection");

            selector.SelectedItem = FindItem();
            await context.WaitAsync(() => effects.Saturation == 1.13);
            Check(Equals((selector.SelectedItem as ComboBoxItem)?.Tag, original.Id), "The revised preset could not be selected again");
            Check((double)page.GetPresetParameters()[nameof(UniverseCostumise.Saturation)]!["Value"]! == 1.13,
                "Reselecting the revised preset did not update the displayed parameters");
            context.Evidence["PresetCatalogRefreshVerified"] = true;
        }
        finally { await store.DeleteAsync(original.Id); }
    }

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
        public double? Saturation { get; private set; }
        public void UpdateEffectValue<T>(EffectValueChanged<T> value) {
            if (value.PropertyName == "AtmosphereIntensity") Intensity = Convert.ToDouble(value.Value);
            if (value.PropertyName == nameof(UniverseCostumise.Saturation)) Saturation = Convert.ToDouble(value.Value);
        }
    }

    private sealed class RecordingApply : IApplyService {
        public ApplyEventArgs? Args { get; private set; }
        public void OnApply(ApplyEventArgs args) => Args = args;
    }
}
