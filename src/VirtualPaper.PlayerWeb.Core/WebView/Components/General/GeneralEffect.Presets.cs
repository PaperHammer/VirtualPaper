using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Events;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.PlayerWeb.Core.WebView.Components.General;

public sealed partial class GeneralEffect {
    public event EventHandler? ParametersChanged;
    private bool _presetEditorMode;
    private bool _presetReadOnly;
    private readonly List<ContentControl> _presetParameterHosts = [];
    private RuntimeType _presetRuntimeType;
    private bool _rebuildingParameters, _loadingPresets, _refreshingPresets;
    private string? _selectedPresetId;
    private JsonObject? _beforePreset;
    private ArcWindow? _presetOwner;
    private RuntimeType CurrentRuntimeType => _presetEditorMode ? _presetRuntimeType
        : Enum.TryParse<RuntimeType>(_startArgs?.RuntimeType, out var type) ? type : RuntimeType.RUnknown;

    // Reuse the existing parameter controls without creating a player or reading a wallpaper file.
    public void EditPreset(JsonObject properties, RuntimeType type) {
        _presetEditorMode = true;
        _presetRuntimeType = type;
        _isInitialized = true;
        PresetActions.Visibility = EffectActionButtons.Visibility = Visibility.Collapsed;
        SetParameters(properties, false);
    }

    public JsonObject GetPresetParameters() => (JsonObject)_wpEffectData!.DeepClone();

    // Lock values, not the scrolling or the buttons that reveal grouped parameters.
    public void SetPresetReadOnly(bool isReadOnly) {
        if (_presetReadOnly == isReadOnly) return;
        _presetReadOnly = isReadOnly;
        foreach (var host in _presetParameterHosts) host.IsEnabled = !isReadOnly;
        UpdatePresetSummaryLayout(_atmosphereButton, _atmosphereSummary);
        UpdatePresetSummaryLayout(_parallaxButton, _parallaxSummary);
    }

    private UIElement WrapPresetParameter(UIElement element, bool isParameter) {
        if (!_presetEditorMode || !isParameter || element is not Control) return element;
        // An outer host preserves each control's own enabled state (e.g. no styles).
        var host = new ContentControl {
            Content = element,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsEnabled = !_presetReadOnly
        };
        _presetParameterHosts.Add(host);
        return host;
    }

    private void UpdatePresetSummaryLayout(DropDownButton? button, TextBlock? summary) {
        if (!_presetEditorMode || button == null || summary == null) return;
        bool readOnly = _presetReadOnly;
        button.Width = readOnly ? double.NaN : MIN_WIDTH;
        button.HorizontalAlignment = readOnly ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        summary.TextWrapping = readOnly ? TextWrapping.Wrap : TextWrapping.NoWrap;
        summary.TextTrimming = readOnly ? TextTrimming.None : TextTrimming.CharacterEllipsis;
    }

    private void SetParameters(JsonObject properties, bool notifyPlayer) {
        _rebuildingParameters = true;
        try {
            PreCleanup();
            UnSubscribe();
            _wpEffectData = properties;
            EnsureEffectProperties(properties);
            GenerateUIElements();
        }
        finally { _rebuildingParameters = false; }
        if (notifyPlayer) SendAllEffectValues();
    }

    private void SendAllEffectValues() {
        var properties = _wpEffectData?.AsObject();
        if (properties == null) return;
        foreach (var (name, value) in properties) {
            if (value is not JsonObject property || property["Value"] == null) continue;
            switch (property["Type"]?.ToString().ToLowerInvariant()) {
                case "slider": _effectService?.UpdateEffectValue(new EffectValueChanged<double> { ControlName = "Slider", PropertyName = name, Value = (double)property["Value"]! }); break;
                case "dropdown": _effectService?.UpdateEffectValue(new EffectValueChanged<int> { ControlName = "Dropdown", PropertyName = name, Value = (int)property["Value"]! }); break;
                case "checkbox": _effectService?.UpdateEffectValue(new EffectValueChanged<bool> { ControlName = "Checkbox", PropertyName = name, Value = (bool)property["Value"]! }); break;
            }
        }
    }

    private async void PresetPage_Loaded(object sender, RoutedEventArgs args) {
        LanguageUtil.LanguageUpdated -= PresetLanguageUpdated;
        LanguageUtil.LanguageUpdated += PresetLanguageUpdated;
        if (!_presetEditorMode) await RefreshPresetsAsync();
    }

    private void PresetPage_Unloaded(object sender, RoutedEventArgs args) => LanguageUtil.LanguageUpdated -= PresetLanguageUpdated;

    private async void PresetLanguageUpdated(object? sender, EventArgs args) {
        if (_wpEffectData != null) SetParameters(_wpEffectData.AsObject(), false);
        if (!_presetEditorMode) await RefreshPresetsAsync();
    }

    private async void PresetSelector_DropDownOpened(object sender, object args) => await RefreshPresetsAsync();

    private async System.Threading.Tasks.Task RefreshPresetsAsync() {
        if (_refreshingPresets || _presetEditorMode || _isDisposed) return;
        _refreshingPresets = true;
        try {
            var presets = await new EffectPresetStore().LoadAsync();
            if (_isDisposed || _presetEditorMode || !IsLoaded) return;
            _loadingPresets = true;
            var compatible = presets.Where(item => EffectPresets.CanApply(item, CurrentRuntimeType)).ToArray();
            var previous = PresetSelector.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => Equals(item.Tag, _selectedPresetId))?.DataContext as EffectPreset;
            var refreshed = compatible.FirstOrDefault(item => item.Id == _selectedPresetId);
            // Compare before replacing item data. A revised preset is no longer the
            // live effect; let the user choose it again without changing the trial snapshot.
            if (previous != null && refreshed != null && !EffectPresets.HaveSameParameters(previous, refreshed))
                _selectedPresetId = null;
            var ids = new string?[] { null, "default" }.Concat(compatible.Select(item => item.Id));
            // Opening an unchanged catalog must not clear the live dropdown's items
            // and reset its selection. Refresh labels/data in place instead.
            if (!PresetSelector.Items.OfType<ComboBoxItem>().Select(item => item.Tag as string).SequenceEqual(ids)) {
                PresetSelector.Items.Clear();
                // Custom describes manual values; it is not an actionable preset.
                PresetSelector.Items.Add(new ComboBoxItem { IsEnabled = false });
                PresetSelector.Items.Add(new ComboBoxItem { Tag = "default" });
                foreach (var preset in compatible) PresetSelector.Items.Add(new ComboBoxItem { Tag = preset.Id });
            }
            ((ComboBoxItem)PresetSelector.Items[0]).Content = LanguageUtil.GetI18n("EffectPreset_Custom");
            ((ComboBoxItem)PresetSelector.Items[1]).Content = LanguageUtil.GetI18n("EffectPreset_Default");
            for (int i = 0; i < compatible.Length; i++) {
                var preset = compatible[i];
                var item = (ComboBoxItem)PresetSelector.Items[i + 2];
                item.Content = preset.IsBuiltIn ? LanguageUtil.GetI18n(preset.Name) : preset.Name;
                item.DataContext = preset;
            }
            var selected = PresetSelector.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, _selectedPresetId));
            _selectedPresetId = selected?.Tag as string;
            PresetSelector.SelectedItem = selected ?? PresetSelector.Items[0];
        }
        catch (Exception ex) { ArcLog.GetLogger<GeneralEffect>().Error(ex); GlobalMessageUtil.ShowError("EffectPreset_LoadError", isNeedLocalizer: true); }
        finally { _loadingPresets = false; _refreshingPresets = false; }
    }

    private void PresetSelector_SelectionChanged(object sender, SelectionChangedEventArgs args) {
        if (_loadingPresets || _rebuildingParameters || _wpEffectData == null
            || PresetSelector.SelectedItem is not ComboBoxItem { Tag: string id } item || id == _selectedPresetId) return;
        string? previousId = _selectedPresetId;
        try {
            var preset = item.DataContext as EffectPreset;
            if (Equals(item.Tag, "default")) {
                var defaults = System.IO.File.Exists(_startArgs.WpEffectFilePathTemplate)
                    ? VirtualPaper.Common.Utils.Storage.JsonNodeUtil.GetWritableJson(_startArgs.WpEffectFilePathTemplate).AsObject()
                    : EffectPresets.Defaults(CurrentRuntimeType);
                EnsureEffectProperties(defaults);
                preset = EffectPresets.Capture("Default", CurrentRuntimeType, defaults);
            }
            var properties = EffectPresets.Apply(_wpEffectData.AsObject(), preset!, CurrentRuntimeType);
            _beforePreset ??= (JsonObject)_wpEffectData.DeepClone();
            _selectedPresetId = id;
            SetParameters(properties, true);
            CancelPreset.Visibility = Visibility.Visible;
        }
        catch (Exception ex) {
            _selectedPresetId = previousId;
            SelectCurrentPreset();
            ArcLog.GetLogger<GeneralEffect>().Error(ex);
            GlobalMessageUtil.ShowError("EffectPreset_ApplyError", isNeedLocalizer: true);
        }
    }

    private void CancelPreset_Click(object sender, RoutedEventArgs args) {
        if (_beforePreset != null) SetParameters(_beforePreset, true);
        EndPresetTrial();
    }

    private void EndPresetTrial() {
        _beforePreset = null;
        CancelPreset.Visibility = Visibility.Collapsed;
        ShowCustomPresetSelection();
    }

    private void ShowCustomPresetSelection() {
        if (_presetEditorMode || _loadingPresets) return;
        _selectedPresetId = null;
        SelectCurrentPreset();
    }

    private void SelectCurrentPreset() {
        _loadingPresets = true;
        try {
            PresetSelector.SelectedItem = PresetSelector.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => Equals(item.Tag, _selectedPresetId));
        }
        finally { _loadingPresets = false; }
    }

    private void AttachPresetOwner(FrameworkPayload payload) {
        if (_presetOwner != null) _presetOwner.Closed -= PresetOwner_Closed;
        payload.TryGet(NaviPayloadKey.ArcWindow.ToString(), out _presetOwner);
        if (_presetOwner != null) _presetOwner.Closed += PresetOwner_Closed;
    }

    private void PresetOwner_Closed(object sender, WindowEventArgs args) {
        // AdjustConfig already reloads saved effects on close; a preview player is destroyed.
        // Do not send a second, possibly stale snapshot after that owner's reload command.
        Dispose();
    }

    private void DetachPresetEvents() {
        LanguageUtil.LanguageUpdated -= PresetLanguageUpdated;
        if (_presetOwner != null) _presetOwner.Closed -= PresetOwner_Closed;
        _presetOwner = null;
        ParametersChanged = null;
    }
}
