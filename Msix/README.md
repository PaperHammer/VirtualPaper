# Core-first MSIX packaging

[简体中文](README.zh-CN.md)

The separate `VirtualPaper.Package.wapproj` packages Core plus the existing `Plugins/UI`, `Plugins/PlayerWeb`, `Plugins/ScrSaver`, `Plugins/ML`, `Plugins/Shaders` and `BuiltInWallpapers` layout. Core is the Start-menu/startup entry point and launches the other processes normally. The WAP project is not part of the normal solution build; the existing Inno workflow remains the EXE release channel.

From the repository root, with Windows, PowerShell 7.2+, Visual Studio MSBuild and MSIX Packaging Tools/Windows SDK 10.0.26100:

```powershell
pwsh -NoProfile -File .github/scripts/Build-Msix.ps1
# After a complete Release solution build:
pwsh -NoProfile -File .github/scripts/Build-Msix.ps1 -SkipSolutionBuild
```

The script rebuilds the Release solution using its existing Any CPU mappings, publishes four processes with self-contained .NET/Windows App SDK runtimes, stages the WinUI resources, packages, and checks the archive. Publishing redirects process build outputs into `Msix/obj`, rejects stale self-contained CLR files in framework-dependent EXE output, and verifies normal output fingerprints stay unchanged. Properties are invocation-local; leave the old UI project's `IsMsixRelease` false. Each output uses a new `Msix/AppPackages` directory. Do not commit generated payloads, packages or certificates.

The default `PaperHammer.VirtualPaper.Preview` / `CN=PaperHammer` / `1.0.0.0` identity is a local preview. The default package is unsigned. For side-loading, pass `-CertificateThumbprint '<40-digit thumbprint>'` and a `-Publisher` matching an existing trusted development certificate. The script does not create/trust certificates, install/register packages or launch the application. Side-loading also needs the declared `Microsoft.VCLibs.140.00.UWPDesktop` framework. Web playback requires the WebView2 Evergreen Runtime on the test machine.

After reserving a product in Partner Center:

```powershell
pwsh -NoProfile -File .github/scripts/Build-Msix.ps1 -Store `
  -IdentityName '<Package/Identity/Name>' -Publisher '<Package/Identity/Publisher>' `
  -PublisherDisplayName '<PublisherDisplayName>' -PackageVersion '1.0.1.0'
```

`-Store` rejects the preview identity and requires publisher fields explicitly. Package version components are at most 65535, major is nonzero and revision is zero; package versions are separate from the product/EXE version. Identity overrides only affect the generated manifest under `obj`. Store re-signs accepted MSIX submissions; a purchased signing certificate is not required.

MSIX disables EXE/plugin patch execution, while releasing startup gates normally. It seeds missing built-in wallpaper folders into the configured writable library before UI startup; entire existing folders are preserved, matching Inno. No user data/settings migration is introduced. WinUI PRI indexes are relocated and merged at package root: embedded XBF, strings and URI names stay intact; file candidates point into `Plugins/UI`. See Microsoft's [PRIINFO indexer](https://learn.microsoft.com/en-us/windows/uwp/app-resources/makepri-exe-format-specific-indexers#priinfo).

`test/Test-MsixPackage.ps1 -PackagePath '<application.msix>'` checks actual package entry/startup, runtime files, translations, models, shaders and resource indexes. It does not prove installation/UI operation. Before Store submission, run WACK and clean-machine installed acceptance: standard-user Start-menu startup, Core/UI IPC, both languages, wallpaper/editing/AI/screen-saver features, startup settings, upgrade/profile retention, normal exit and uninstall. Arrange persistent system-setting cases separately from the regular UI suite. Do not connect acceptance to a running daily instance. Inno smoke/unpackaged UI tests do not replace MSIX acceptance.

Pre-merge now packages EXE and MSIX in parallel and independently verifies both installed formats. Pre-release adds the formal Store channel for a merged PR labeled `channel:msstore`; GitHub receives only the EXE/plugin draft, while Partner Center receives an uploaded, uncommitted MSIX draft. Complete the Store identity/API configuration before dispatching that release. See the [workflow guide](../.github/workflows/README.md). CI consumes an existing package with `Run-MsixTests.ps1 -PackageArtifact <package.json> -Mode Smoke|UI`; all selected checks are strict.

For complete local MSIX regression, run `pwsh -NoProfile -File test/Run-MsixTests.ps1` as administrator on an unlocked desktop. See the [testing guide](../test/README.md). It uses an automatically generated per-run identity/certificate, all eight project test jobs, the actual signed package and the existing extended real UI suite. Installation, first-run automatic UI, reinstallation with a retained profile, a fresh profile, normal exits and uninstall are required. The temporary certificate trust and package are removed afterward; failed or skipped UI cannot produce a passing result. Shell activation carries the existing test session/data-root isolation using explicit `--virtualpaper-test-session` / `--virtualpaper-test-data-root` arguments; normal launches keep their existing behavior, and child processes inherit isolation normally.

Reports include `msix-acceptance.json`, all project TRX/coverage, four raw UI reports and `msix-summary.md`. Revalidate the completed evidence with `pwsh -NoProfile -File test/Summarize-MsixTestResults.ps1 -Root '<result-directory>'`. This rejects missing/skipped phases and rechecks the raw project/UI reports; it does not run missing tests. Focused MSIX UI uses `Run-UIOperationTests.ps1 -PackageFullName '<per-run test package full name>' -NoBuild -Configuration Release`; daily Store/preview identities are refused.

The runtime's physical `Microsoft.WindowsAppRuntime.pri` is also retained in directories containing its runtime DLL. Merging its strings into root `resources.pri` alone is insufficient: the SDK's [PickerLocalization.cpp](https://github.com/microsoft/WindowsAppSDK/blob/release/1.8-stable/dev/Interop/StoragePickers/PickerLocalization.cpp) opens that named file beside its module. The archive check requires these physical files, and installed import/export UI exercises them.

WPF process intermediates are configured before Common.targets creates its items; output paths are redirected afterwards. WinUI retains the MSIX SDK's own early import hook. Packaging rejects missing compiled Core XAML before compression.
