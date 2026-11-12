# Shared localization

[English](README.md) | [简体中文](README.zh-CN.md)

`LanguageCatalog.json` is the shared list of supported locales, native display names,
aliases, installer names and Inno language files. The default is `en-US`.
The catalog includes English, Simplified Chinese, Traditional Chinese, Japanese, Korean,
German, French, Spanish, Brazilian Portuguese and Russian. Every locale contains the
complete source key set, including application, tray and installer messages. Actual
text layout still needs desktop UI review; translating resources does not verify layout.

Maintain all product text once per language in `Strings/<locale>/Resources.resw`.
Existing UI keys retain their names. `Core_`, `Common_FileAccess_` and `Installer_`
keys cover the tray/Core, shared file errors and custom installer messages.
`ResourceMappings.xml` preserves each consumer's existing resource key.
XAML property mappings remain in
[`XamlResources.xml`](../src/VirtualPaper.UIComponent/Localization/XamlResources.xml).
Do not translate user titles, authors, tags, project contents, IDs or serialized enums.

## Generation

```powershell
pwsh -NoProfile -File localization/Generate-Resources.ps1 -OutputRoot TestResults/localization
pwsh -NoProfile -File localization/Generate-Resources.ps1 -OutputRoot TestResults/localization -Check
```

Windows PowerShell 5.1 is also supported. `-Target UI|Core|Common|Installer|Manifest`
generates one consumer directly into the requested output folder; the default `All`
uses separate subfolders. The old UI generation command remains a wrapper.
`-Target Metadata` supplies the native WinUI head's default language without adding
another copy of the UI translations.

Normal MSBuild generates UI/Core/Common resources in each project's `obj` folder.
UI still loads the same `VirtualPaper.UIComponent/Strings` runtime layout through
WinUI3Localizer. Core/Common still use their existing ResourceManager names and
satellite assemblies, with English neutral resources and explicit satellites for other locales.
Inno generates its ignored `obj/Localization` include on every direct compilation.
WAP generates the actual MSIX manifest from the catalog before applying identity/version.
Never compile the manifest template directly; its language declarations are generated.

The Traditional Chinese Inno file is the unmodified official translation by GoneTone
and its credited contributors, downloaded from the [Inno translations page](https://jrsoftware.org/files/istrans/).
The upstream download SHA-256 is `031684fc769259291fd563338b5abe20b7753c88ab5a2976b83a80788deb8455`.
Copyright notices remain in the file and the [Inno Setup license](../InnoSetup/Languages/LICENSE-InnoSetup.txt)
is included alongside it. Other new installer languages use the files bundled with Inno Setup.

The generator rejects duplicate keys/aliases/mappings, missing code-only or mapped
translations, mismatched format argument indexes, empty translations of nonempty
source text, unsupported source folders and unmapped static XAML UIDs. `-Check`
does not repair files. Unchanged outputs keep their timestamps; retired generated
resources are removed without deleting unrelated files or source inputs.

## Language selection

Existing settings are canonicalized and retained across updates. Only new/recovered
settings consult the installer marker and then the ordered Windows language preferences.
New installers write canonical tags; historical `english`/`chinese` markers remain valid.
Regional aliases such as `en-GB` map to their supported parent. Matching does not cross
Chinese writing systems through the neutral `zh` alias. Unknown languages and runtime
missing-resource fallback use English. UI language changes do not replace regional
number/date settings or user-created content.

## Validation

```powershell
pwsh -NoProfile -File test/Test-LocalizationResources.ps1
pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1
```

Contracts use temporary files only and cover all generated consumers, a synthetic third
language, malformed input, source protection, stale output and retired-resource cleanup.
Core tests cover language matching, initial language persistence, upgrade retention and
actual neutral/satellite resources. Artifact tests compare every advertised UI translation
against an embedded source snapshot; MSIX checks its actual language declarations too.
These checks do not replace authorized installed-app or real UI/layout acceptance.
