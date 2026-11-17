# Localization resources

[English](README.md) | [简体中文](README.zh-CN.md)

Maintain each translation once in [`localization/Strings`](../../../localization/Strings). The [shared localization guide](../../../localization/README.md) covers the language catalog, Core/Common resources, installer messages and MSIX declarations. Do not add `Ui_...` entries to source files.

`XamlResources.xml` maps each XAML property resource to a source key, independently of language:

```xml
<Resource Name="Ui_Intelligent_Result_PreservationTooltip.ToolTipService.ToolTip"
          Source="Intelligent_Result_PreservationTooltip" />
```

The UID in XAML is `Ui_Intelligent_Result_PreservationTooltip`; the suffix identifies the target property. Several properties on one control have separate mappings under the same UID.

## Editing and building

1. To change wording, edit the source value in each supported locale. Existing XAML mappings need no changes.
2. For a new localized control/property, add its source key and translations, add one mapping, and set `l:Uids.Uid` in XAML.
3. Build the solution normally. `GenerateLocalizedResources` runs before content paths are assigned, including Visual Studio and both CI workflows. It writes complete resource files under `obj/<configuration>/<target-framework>/Localization/Strings` and copies them to the existing runtime layout. Generated files are excluded from Git.

Generation fails for duplicate keys/mappings, missing mapped translations, manually added `Ui_...` source entries, or a static source XAML UID without a mapping. It scans source XAML while ignoring build outputs. Unchanged generated files keep their timestamps.

From the repository root, generate or verify a separate output directory:

```powershell
powershell -NoProfile -File src/VirtualPaper.UIComponent/Localization/Generate-Resources.ps1 -OutputRoot TestResults/localization-resources
powershell -NoProfile -File src/VirtualPaper.UIComponent/Localization/Generate-Resources.ps1 -OutputRoot TestResults/localization-resources -Check
```

The script supports Windows PowerShell 5.1 and PowerShell 7. `-Check` verifies existing output without updating it. Generation must not target the source `Strings` directory.

## Packaged validation

The `test-artifacts` job checks every catalog locale against embedded source and mapping snapshots. Every source key and mapping must exist, every generated value must match its source, and the generated property count must match the mapping count. Runtime packages need only the generated resource files.

These checks cover resource generation and packaging. Live language switching remains covered by the real WinUI localization case; resource validation alone does not prove UI interaction.
