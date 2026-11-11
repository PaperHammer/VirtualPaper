using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Models.Cores;
using VirtualPaper.PlayerWeb.Core.WebView.Components.General;
using VirtualPaper.UIComponent.Dialogs;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.Utils.Interfaces;

namespace VirtualPaper.WpSettingsPanel.Views;

public sealed partial class EffectPresetManagerDialog : ArcContentDialog {
    private readonly EffectPresetStore _store = new();
    private readonly IWallpaperIndexService _index;
    private readonly GeneralEffect _editor = new();
    private EffectPreset? _draft;
    private WallpaperIndexEntry? _selectedSource;
    private WallpaperSourceQuery? _sourceQuery;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherQueueTimer _sourceSearchTimer;
    private CancellationTokenSource? _sourceLoad, _sourcePickerLoad;
    private EffectPresetListItem[] _rows = [];
    private EffectPresetListItem? _savedSelection, _returnSelection;
    private string? _deleteTargetId;
    private bool _updating, _dirty, _busy, _writing, _closed, _choosingWallpaper;
    private int _sourceVersion, _sourceListVersion;
    private static string T(string key) => LanguageUtil.GetI18n(key);
    private static string PresetLabel(EffectPreset preset) => preset.IsBuiltIn ? T(preset.Name) : preset.Name;
    private static string TypeLabel(RuntimeType type) => T("EffectPreset_Type_" + type);
    private static EffectPresetListItem ListItem(EffectPreset preset) => new(preset, PresetLabel(preset), T(preset.IsBuiltIn ? "EffectPreset_BuiltIn" : "EffectPreset_User"));
    private bool IsNewDraft => _draft?.IsBuiltIn == false && _savedSelection == null;
    private bool CanSave => !_busy && !_choosingWallpaper && _draft?.IsBuiltIn == false
        && (_dirty || IsNewDraft) && !string.IsNullOrWhiteSpace(PresetName.Text);
    private EffectPresetListItem? FindRow(string? id) => _rows.FirstOrDefault(item => item.Preset.Id == id);

    public EffectPresetManagerDialog(IWallpaperIndexService index) {
        _index = index;
        InitializeComponent();
        _sourceSearchTimer = DispatcherQueue.CreateTimer();
        _sourceSearchTimer.Interval = TimeSpan.FromMilliseconds(150);
        _sourceSearchTimer.IsRepeating = false;
        _sourceSearchTimer.Tick += SourceSearch_Tick;
        UpdateSourceName();
        _editor.EditPreset(EffectPresets.Defaults(RuntimeType.RImage), RuntimeType.RImage);
        EditorHost.Content = _editor;
        _editor.ParametersChanged += Parameters_Changed;
        foreach (var type in EffectPresets.Types) TypePicker.Items.Add(new ComboBoxItem { Content = TypeLabel(type), Tag = type });
        SetBusy(true);
    }

    protected override void OnApplyTemplate() {
        base.OnApplyTemplate();
        foreach (string name in new[] { "PrimaryButton", "SecondaryButton", "CloseButton" })
            if (GetTemplateChild(name) is Button button) button.MinWidth = 128;
        UpdateActions();
    }

    private async void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) {
        LanguageUtil.LanguageUpdated += Language_Updated;
        try {
            await ReloadAsync();
        }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception ex) { ShowError(ex, "EffectPreset_LoadError"); }
        finally { if (!_closed) SetBusy(false); }
    }

    private async Task ReloadAsync(string? selectedId = null) {
        var presets = await _store.LoadAsync().WaitAsync(_lifetime.Token);
        if (_closed) return;
        selectedId ??= _savedSelection?.Preset.Id ?? _returnSelection?.Preset.Id;
        _updating = true;
        try {
            _rows = presets.Select(ListItem).ToArray();
            PresetList.ItemsSource = _rows;
        }
        finally { _updating = false; }
        var fallback = _rows.FirstOrDefault(item => !item.Preset.IsBuiltIn) ?? _rows.First();
        Select(FindRow(selectedId) ?? fallback);
        PresetList.ScrollIntoView(_savedSelection);
    }

    private void Select(EffectPresetListItem? row) {
        if (row != null) row = FindRow(row.Preset.Id) ?? row;
        _savedSelection = row;
        _returnSelection = row;
        _draft = row?.Preset;
        if (row != null) { ShowDraft(); return; }
        _dirty = false;
        _updating = true;
        try {
            PresetList.SelectedItem = null;
            PresetName.Text = "";
            TypePicker.SelectedIndex = -1;
        }
        finally { _updating = false; }
        SetSourceMode(false);
    }

    private void BeginDraft(EffectPreset preset) {
        _returnSelection = _savedSelection ?? _returnSelection;
        _savedSelection = null;
        _draft = preset;
        ShowDraft();
    }

    private void ShowDraft() {
        if (_draft == null) return;
        // Creating/loading a draft is not a user edit; only input events mark it dirty.
        _dirty = false;
        SetSourceMode(false);
        _updating = true;
        try {
            // A draft is not a saved library entry. New/Copy leave the list unselected.
            PresetList.SelectedItem = _savedSelection;
            PresetName.Text = PresetLabel(_draft);
            TypePicker.SelectedItem = TypePicker.Items.Cast<ComboBoxItem>().First(item => Equals(item.Tag, _draft.RuntimeType));
            _editor.EditPreset(EffectPresets.Apply(EffectPresets.Defaults(_draft.RuntimeType), _draft, _draft.RuntimeType), _draft.RuntimeType);
            Notice.IsOpen = false;
        }
        finally { _updating = false; }
        UpdateActions();
        if (IsNewDraft) {
            PresetName.Focus(FocusState.Programmatic);
            PresetName.SelectAll();
        }
    }

    private void Preset_SelectionChanged(object sender, SelectionChangedEventArgs args) {
        if (_updating || _closed || _busy) return;
        if (PresetList.SelectedItem is EffectPresetListItem selected) Select(selected);
    }

    private void New_Click(object sender, RoutedEventArgs args) {
        if (_busy) return;
        BeginDraft(EffectPresets.Capture(T("EffectPreset_NewName"), RuntimeType.RImage, EffectPresets.Defaults(RuntimeType.RImage)));
    }

    private void NewFromWallpaper_Click(object sender, RoutedEventArgs args) {
        if (_busy) return;
        SetSourceMode(true);
        Notice.IsOpen = false;
        SourcePickerButton.Focus(FocusState.Programmatic);
    }

    private void CancelSource_Click(object sender, RoutedEventArgs args) {
        if (_busy) return;
        SetSourceMode(false);
        Notice.IsOpen = false;
    }

    private void SetSourceMode(bool choosing) {
        DeleteFlyout.Hide();
        _choosingWallpaper = choosing;
        SourcePanel.Visibility = choosing ? Visibility.Visible : Visibility.Collapsed;
        PresetWorkspace.Visibility = choosing ? Visibility.Collapsed : Visibility.Visible;
        if (!choosing) {
            SourceFlyout.Hide();
            _sourceVersion++;
            _sourceLoad?.Cancel();
            _sourceLoad?.Dispose();
            _sourceLoad = null;
            _selectedSource = null;
            RuntimePicker.ItemsSource = null;
            UpdateSourceName();
        }
        UpdateActions();
    }

    private void Copy_Click(object sender, RoutedEventArgs args) {
        if (_busy || _savedSelection == null) return;
        var source = _savedSelection.Preset;
        string suffix = T("EffectPreset_CopySuffix");
        string name = PresetLabel(source);
        BeginDraft(EffectPresets.Capture(name[..Math.Min(name.Length, 80 - suffix.Length)] + suffix,
            source.RuntimeType, EffectPresets.Apply(EffectPresets.Defaults(source.RuntimeType), source, source.RuntimeType)));
    }

    private async void Save_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args) {
        args.Cancel = true;
        if (!CanSave) return;
        var deferral = args.GetDeferral();
        _writing = true;
        SetBusy(true);
        try {
            var preset = EffectPresets.Capture(PresetName.Text, _draft!.RuntimeType, _editor.GetPresetParameters(), _draft.Id);
            if (_rows.Any(row => !row.Preset.IsBuiltIn && row.Preset.Id != preset.Id
                && row.Preset.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase))) {
                Notice.Message = T("EffectPreset_DuplicateName"); Notice.Severity = InfoBarSeverity.Warning; Notice.IsOpen = true;
                return;
            }
            await _store.SaveAsync(preset);
            _dirty = false;
            await ReloadAsync(preset.Id);
        }
        catch (Exception ex) { ShowError(ex, "EffectPreset_SaveError"); }
        finally { _writing = false; if (!_closed) SetBusy(false); deferral.Complete(); }
    }

    private void DeleteFlyout_Opened(object? sender, object args) {
        if (_busy || _closed || _savedSelection?.Preset.IsBuiltIn != false) {
            DeleteFlyout.Hide();
            return;
        }
        _deleteTargetId = _savedSelection.Preset.Id;
        DeleteTargetName.Text = _savedSelection.Label;
        ToolTipService.SetToolTip(DeleteTargetName, DeleteTargetName.Text);
    }

    private void DeleteFlyout_Closed(object? sender, object args) => _deleteTargetId = null;

    private void CancelDelete_Click(object sender, RoutedEventArgs args) => DeleteFlyout.Hide();

    private async void ConfirmDelete_Click(object sender, RoutedEventArgs args) {
        string? id = _deleteTargetId;
        if (_busy || _closed || id == null || _savedSelection?.Preset.IsBuiltIn != false || _savedSelection.Preset.Id != id) return;
        DeleteFlyout.Hide();
        _writing = true;
        SetBusy(true);
        try { await _store.DeleteAsync(id); await ReloadAsync(); }
        catch (Exception ex) { ShowError(ex, "EffectPreset_SaveError"); }
        finally { _writing = false; if (!_closed) SetBusy(false); }
    }

    private void Discard_Click(object sender, RoutedEventArgs args) {
        if (_busy) return;
        Select(_savedSelection ?? _returnSelection ?? _rows.FirstOrDefault());
    }

    // TextChanging runs synchronously inside ShowDraft's update guard. TextChanged
    // can arrive later and mistake a loaded name for an edit.
    private void Name_Changing(TextBox sender, TextBoxTextChangingEventArgs args) => MarkDirty(updateActions: false);
    // Refresh visual state after rendering, when changing the visual tree is safe.
    private void Name_Changed(object sender, TextChangedEventArgs args) => UpdateActions();
    private void Parameters_Changed(object? sender, EventArgs args) => MarkDirty();
    private void MarkDirty(bool updateActions = true) {
        if (_updating || _draft == null || _draft.IsBuiltIn) return;
        _dirty = true;
        if (updateActions) UpdateActions();
    }

    private void Type_Changed(object sender, SelectionChangedEventArgs args) {
        if (_updating || _draft == null || _draft.IsBuiltIn
            || TypePicker.SelectedItem is not ComboBoxItem { Tag: RuntimeType type } || type == _draft.RuntimeType) return;
        try {
            var common = EffectPresets.Capture("Common", RuntimeType.RUnknown, _editor.GetPresetParameters());
            _draft = _draft with { RuntimeType = type };
            _editor.EditPreset(EffectPresets.Apply(EffectPresets.Defaults(type), common, type), type);
            MarkDirty();
        }
        catch (Exception ex) { ShowError(ex, "EffectPreset_ApplyError"); }
    }

    private async void SourceFlyout_Opened(object? sender, object args) {
        if (_closed || !_choosingWallpaper) return;
        int version = ++_sourceListVersion;
        _sourcePickerLoad?.Cancel();
        _sourcePickerLoad?.Dispose();
        _sourcePickerLoad = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _sourcePickerLoad.Token;
        SourceLoading.IsActive = true;
        SourceEmpty.Visibility = Visibility.Collapsed;
        SourceSearch.Focus(FocusState.Programmatic);
        try {
            // Index readiness belongs to this optional picker, never to the dialog's close gate.
            await _index.Initialized.Task.WaitAsync(token);
            var query = await Task.Run(() => new WallpaperSourceQuery(_index.Query(0, int.MaxValue),
                uid => _index.GetOrganization(uid).Favorite), token);
            if (_closed || version != _sourceListVersion) return;
            _sourceQuery = query;
            RefreshSources();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (!_closed && version == _sourceListVersion) ShowError(ex, "EffectPreset_LoadError"); }
        finally { if (!_closed && version == _sourceListVersion) SourceLoading.IsActive = false; }
    }

    private void SourceSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) {
        if (_closed || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _sourceSearchTimer.Stop();
        _sourceSearchTimer.Start();
    }

    private void SourceSearch_Tick(DispatcherQueueTimer sender, object args) => RefreshSources();

    private void SourceFavorites_Changed(object sender, RoutedEventArgs args) => RefreshSources();

    private void SourceFlyout_Closed(object? sender, object args) {
        _sourceListVersion++;
        _sourcePickerLoad?.Cancel();
        _sourcePickerLoad?.Dispose();
        _sourcePickerLoad = null;
        _sourceSearchTimer.Stop();
        _sourceQuery = null;
        SourcePicker.ItemsSource = null;
        SourceLoading.IsActive = false;
    }

    private void RefreshSources() {
        if (_closed || _sourceQuery == null || SourcePicker == null) return;
        var items = _sourceQuery.Search(SourceSearch.Text, SourceFavorites.IsChecked == true);
        SourcePicker.ItemsSource = items;
        // Filtering changes only the visible list, never the committed source or its runtime configuration.
        SourcePicker.SelectedItem = items.FirstOrDefault(item => item.Uid.Equals(_selectedSource?.Uid, StringComparison.OrdinalIgnoreCase));
        SourceEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSourceName() {
        SourceName.Text = _selectedSource?.DisplayName ?? T("EffectPreset_SelectWallpaper");
        ToolTipService.SetToolTip(SourcePickerButton, SourceName.Text);
    }

    private void SourceThumbnail_ImageFailed(object sender, ExceptionRoutedEventArgs args) {
        if (sender is Image { Parent: Grid host } && host.FindName("SourceNoPreview") is UIElement placeholder)
            placeholder.Visibility = Visibility.Visible;
    }

    private async void Source_ItemClick(object sender, ItemClickEventArgs args) {
        if (_busy || _closed || !_choosingWallpaper || args.ClickedItem is not WallpaperIndexEntry entry) return;
        _selectedSource = entry;
        UpdateSourceName();
        SourceFlyout.Hide();
        int version = ++_sourceVersion;
        _sourceLoad?.Cancel();
        _sourceLoad?.Dispose();
        _sourceLoad = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _sourceLoad.Token;
        RuntimePicker.ItemsSource = null;
        UpdateActions();
        try {
            var files = await Task.Run(() => Directory.Exists(entry.FolderPath)
                ? Directory.EnumerateFiles(entry.FolderPath, Constants.Field.WpRuntimeDataFileName, SearchOption.AllDirectories)
                    .Select(path => { token.ThrowIfCancellationRequested(); return path; }).ToArray() : [], token);
            var rows = new List<EffectPresetRuntimeItem>();
            foreach (string path in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)) {
                if (_closed || version != _sourceVersion) return;
                try {
                    var runtime = await WpRuntimeData.LoadAsync(path).WaitAsync(token);
                    if (runtime.RType != RuntimeType.RUnknown && EffectPresets.Types.Contains(runtime.RType) && File.Exists(runtime.WpEffectFilePathUsing))
                        rows.Add(new EffectPresetRuntimeItem(runtime, $"{TypeLabel(runtime.RType)} · {runtime.MonitorContent}"));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex) { ArcLog.GetLogger<EffectPresetManagerDialog>().Warn(ex.Message); }
            }
            if (_closed || version != _sourceVersion) return;
            RuntimePicker.ItemsSource = rows;
            RuntimePicker.SelectedIndex = rows.Count > 0 ? 0 : -1;
            if (rows.Count == 0) { Notice.Message = T("EffectPreset_NoSource"); Notice.Severity = InfoBarSeverity.Informational; Notice.IsOpen = true; }
            UpdateActions();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (!_closed && version == _sourceVersion) ShowError(ex, "EffectPreset_LoadError"); }
    }

    private void Runtime_SelectionChanged(object sender, SelectionChangedEventArgs args) => UpdateActions();

    private void FromWallpaper_Click(object sender, RoutedEventArgs args) {
        if (!_busy && RuntimePicker.SelectedItem is EffectPresetRuntimeItem row) CreateFromWallpaper(row);
    }

    private async void CreateFromWallpaper(EffectPresetRuntimeItem row) {
        var token = _lifetime.Token;
        SetBusy(true);
        try {
            var properties = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(row.Runtime.WpEffectFilePathUsing, token))!.AsObject();
            if (_closed) return;
            string name = _selectedSource?.DisplayName ?? T("EffectPreset_NewName");
            if (string.IsNullOrWhiteSpace(name)) name = T("EffectPreset_NewName");
            var preset = EffectPresets.Capture(name.Length > 80 ? name[..80] : name, row.Runtime.RType, properties);
            BeginDraft(preset);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { ShowError(ex, "EffectPreset_LoadError"); }
        finally { if (!_closed) SetBusy(false); }
    }

    private void UpdateActions() {
        if (_closed || ManagerHint == null) return;
        bool builtIn = _draft?.IsBuiltIn == true;
        bool editable = _draft != null && !_draft.IsBuiltIn && !_busy && !_choosingWallpaper;
        PresetName.Visibility = builtIn ? Visibility.Collapsed : Visibility.Visible;
        ReadOnlyNamePanel.Visibility = builtIn ? Visibility.Visible : Visibility.Collapsed;
        if (builtIn) ReadOnlyPresetName.Text = PresetLabel(_draft!);
        PresetName.IsReadOnly = !editable;
        TypePicker.IsEnabled = editable;
        _editor.SetPresetReadOnly(!editable);
        EditorHost.Visibility = _draft == null ? Visibility.Collapsed : Visibility.Visible;
        IsPrimaryButtonEnabled = CanSave;
        NewPreset.IsEnabled = MoreActions.IsEnabled = !_busy;
        CopyPreset.IsEnabled = !_busy && !_choosingWallpaper && _savedSelection != null;
        DeletePreset.IsEnabled = !_busy && !_choosingWallpaper && _savedSelection?.Preset.IsBuiltIn == false;
        Discard.IsEnabled = !_busy && (_dirty || IsNewDraft);
        PresetList.IsEnabled = SourcePickerButton.IsEnabled = RuntimePicker.IsEnabled = !_busy;
        FromWallpaper.IsEnabled = !_busy && RuntimePicker.SelectedItem is EffectPresetRuntimeItem;
        CancelSource.IsEnabled = !_busy;
        RuntimePicker.Visibility = RuntimePicker.Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        // Both read-only and unsaved states fit in the same fixed-height toolbar slot.
        bool showStatus = _writing || _dirty || builtIn;
        StateNotice.Visibility = showStatus ? Visibility.Visible : Visibility.Collapsed;
        StateNotice.Style = (Style)Resources[_dirty && !_writing ? "PresetWarningStatusStyle" : "PresetInfoStatusStyle"];
        StateIcon.Glyph = _writing ? "\uE74E" : _dirty ? "\uE7BA" : "\uE946";
        StateText.Text = showStatus ? T(_writing ? "EffectPreset_Saving"
            : _dirty ? _savedSelection == null ? "EffectPreset_Draft" : "EffectPreset_Modified" : "EffectPreset_ReadOnly") : "";
        ToolTipService.SetToolTip(StateNotice, showStatus ? T(_writing ? "EffectPreset_Saving"
            : _dirty ? "EffectPreset_UnsavedHint" : "EffectPreset_ReadOnlyHint") : null);
        PrimaryButtonText = T("EffectPreset_Save");
        CloseButtonText = T("Text_Close");
        if (GetTemplateChild("CloseButton") is Button close) close.IsEnabled = !_writing;
    }

    private void SetBusy(bool busy) { _busy = busy; UpdateActions(); }
    private void ShowError(Exception ex, string key) {
        ArcLog.GetLogger<EffectPresetManagerDialog>().Error(ex);
        if (_closed) return;
        Notice.Severity = InfoBarSeverity.Error;
        Notice.Message = T(key);
        Notice.IsOpen = true;
    }

    private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args) {
        if (_writing) {
            args.Cancel = true;
        }
    }

    private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args) {
        _closed = true;
        _lifetime.Cancel();
        _sourceLoad?.Cancel();
        _sourceLoad?.Dispose();
        _sourceLoad = null;
        _sourcePickerLoad?.Cancel();
        _sourcePickerLoad?.Dispose();
        _sourcePickerLoad = null;
        _sourceSearchTimer.Stop();
        _sourceSearchTimer.Tick -= SourceSearch_Tick;
        SourceFlyout.Hide();
        DeleteFlyout.Hide();
        SourcePicker.ItemsSource = null;
        RuntimePicker.ItemsSource = null;
        PresetList.ItemsSource = null;
        _rows = [];
        _sourceQuery = null;
        _selectedSource = null;
        _draft = null;
        _savedSelection = _returnSelection = null;
        _sourceVersion++;
        LanguageUtil.LanguageUpdated -= Language_Updated;
        _editor.ParametersChanged -= Parameters_Changed;
        _editor.Dispose();
        EditorHost.Content = null;
        _lifetime.Dispose();
    }

    private void Language_Updated(object? sender, EventArgs args) {
        if (_closed) return;
        UpdateSourceName();
        foreach (var item in TypePicker.Items.Cast<ComboBoxItem>()) item.Content = TypeLabel((RuntimeType)item.Tag);
        // Retain an unsaved draft while replacing localized built-in list labels.
        _updating = true;
        try {
            if (_draft?.IsBuiltIn == true) PresetName.Text = PresetLabel(_draft);
            _rows = _rows.Select(row => ListItem(row.Preset)).ToArray();
            PresetList.ItemsSource = _rows;
            _savedSelection = FindRow(_savedSelection?.Preset.Id);
            _returnSelection = FindRow(_returnSelection?.Preset.Id);
            PresetList.SelectedItem = _savedSelection;
            int runtimeIndex = RuntimePicker.SelectedIndex;
            if (RuntimePicker.ItemsSource is IEnumerable<EffectPresetRuntimeItem> runtimes) {
                RuntimePicker.ItemsSource = runtimes.Select(row => row with { Label = $"{TypeLabel(row.Runtime.RType)} · {row.Runtime.MonitorContent}" }).ToArray();
                RuntimePicker.SelectedIndex = runtimeIndex;
            }
        }
        finally { _updating = false; }
        UpdateActions();
    }
}
