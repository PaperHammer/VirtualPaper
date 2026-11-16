using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Models.Cores;

namespace VirtualPaper.WpSettingsPanel.Utils;

// Public row types allow compiled XAML bindings without reflection over dialog-private records.
public sealed record EffectPresetListItem(EffectPreset Preset, string Label, string Kind);
public sealed record EffectPresetRuntimeItem(WpRuntimeData Runtime, string Label);
