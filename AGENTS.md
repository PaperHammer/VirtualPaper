# Working on VirtualPaper

## Project map

VirtualPaper is a Windows wallpaper application built with .NET 8, WPF, WinUI 3 and WebView2. It is a multi-process application: the WPF Core owns services and the tray; the WinUI process provides the library, settings and editors; wallpaper players and the screen saver run separately. Processes communicate through gRPC over named pipes.

| Area | Source entry points |
| --- | --- |
| Solution and Core composition | `src/VirtualPaper.sln`, `src/VirtualPaper/App.xaml.cs` |
| WinUI composition and windows | `src/VirtualPaper.UI/App.xaml.cs`, `src/VirtualPaper.UI/MainWindow.xaml.cs` |
| Settings and wallpaper library | `src/VirtualPaper.AppSettingsPanel`, `src/VirtualPaper.WpSettingsPanel` |
| Image/Web editors and runtime selection | `src/StaticImg`, `src/WebBackdrop`, `src/Workloads.Entry` |
| AI tasks and inference | `src/VirtualPaper.IntelligentPanel`, `src/VirtualPaper.ML` |
| Wallpaper playback and screen saver | `src/VirtualPaper.PlayerWeb`, `src/VirtualPaper.PlayerWeb.Core`, `src/VirtualPaper.ScreenSaver` |
| Shared models, infrastructure and RPC | `src/VirtualPaper.Common`, `src/VirtualPaper.Models`, `src/VirtualPaper.UIComponent`, `src/VirtualPaper.Grpc.*` |
| Packaging and automation | `InnoSetup/setup.iss`, `test`, `.github/scripts`, `.github/workflows` |

Read [developer documentation](doc/README.md) for architecture and module details. Read source implementations when deciding behavior; generated output is not the source of truth.

Maintain translations only in `src/VirtualPaper.UIComponent/Strings/<locale>/Resources.resw`; add XAML property mappings in `Localization/XamlResources.xml`. Normal builds generate complete resources under `obj`. Do not hand-edit or commit generated `Ui_...` translations. See the [localization guide](src/VirtualPaper.UIComponent/Localization/README.md) for generation and packaged validation.

## Test skill and entry points

For requests to run, select, extend or diagnose tests, read the repository skill at [.agents/skills/virtualpaper-testing/SKILL.md](.agents/skills/virtualpaper-testing/SKILL.md). Follow its linked guides only for the relevant test layer. Agents without skill discovery can read that file directly.

- Complete local acceptance: `pwsh -NoProfile -File test/Run-Tests.ps1` from an elevated PowerShell on a clean Windows test installation.
- Focused project tests: `test/Run-ProjectTests.ps1` after a complete Release solution build.
- Real UI tests: `test/Run-UIOperationTests.ps1`; application cases start isolated Core and use its production UI launch path.
- Complete MSIX acceptance: `test/Run-MsixTests.ps1`; requires elevation and an unlocked desktop, uses a per-run signed package identity, all project jobs and installed extended UI, then reinstalls/uninstalls and removes temporary certificate trust. MSIX UI is strict; Inno acceptance remains separate.
- Workflow/script contracts: `pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1`.
- Details and prerequisites: [testing guide](test/README.md) / [中文](test/README.zh-CN.md).

Complete local acceptance follows the current scripts: workflow contracts → Release rebuild → eight parallel project jobs → ISS package → mandatory installer smoke (English install/startup/normal exit → Chinese same-package reinstall → preserved/fresh-profile startup/normal exits → uninstall) → independent extended UI against the Release build → summary. Smoke failures or skipped startup/profile checks block both CI workflows. Local UI remains strict by default. A focused test run does not replace this acceptance.

Use Visual Studio MSBuild on `src/VirtualPaper.sln` with `Configuration=Release` and `Platform="Any CPU"`. Solution mappings supply the individual project architectures. Building a WinUI test host alone with a different platform can produce incompatible dependency copies; rebuild the complete solution when host/application hashes differ.

## Workflow boundaries

[Pre-merge Check](.github/workflows/pre-merge-check.yml) and [Pre-release](.github/workflows/pre-release.yml) are both `workflow_dispatch` only. Local testing does not dispatch them, update PR status, synchronize product versions or create releases.

Pre-merge Check tests a fixed merge snapshot for an open same-repository PR. `dev → main` requires exactly one `version:major`, `version:minor`, `version:build` or `version:revision` label. Pre-release verifies that checked merged PR and retests its versioned build/package. Both workflows require installer smoke and project checks to pass. Extended UI runs in an independent `test-ui-operation` job against Release output, with its own Core/UI sessions; only that job is non-blocking. Summaries retain failed/skipped UI evidence with `-AllowUIFailure -UIJobResult`. Smoke cannot pass with skipped startup/profile checks. Version synchronization/drafts require packaging and validated summary success. Plugin-only releases skip product-version synchronization. See the [workflow guide](.github/workflows/README.md) / [中文](.github/workflows/README.zh-CN.md).

When changing runners, test registration or CI dependencies, update the test/workflow guides and run the workflow contract validator. Test inventories and job mappings live in `test/TestReportSupport.ps1` and `test/VirtualPaper.UIOperationTest/App.xaml.cs`; do not infer counts from old logs.

## Local execution boundaries

Installer acceptance changes real installed files, shortcuts and uninstall registration. Honor the user's authorization for those changes; the scripts reject an existing daily installation. Do not bypass that guard or remove a daily installation/user data without authorization. Isolated application cases must not connect to the user's running instance.

Real UI acceptance requires the active, unlocked Windows desktop and may occupy the foreground. An unavailable desktop is an explicit **skip**, not a passing UI result. Do not weaken detection to make a run green. Session locking, startup registration and persistent system-setting UI cases remain excluded; real idle testing is opt-in.

Keep `TestResults`, `bin`, `obj`, `Plugins`, installer output and downloaded/generated third-party assets out of commits. Root `scripts/` is ignored local tooling; tracked reusable automation belongs under `test/` or `.github/scripts/`. Describe actual validation, skipped coverage and remaining failures separately.
