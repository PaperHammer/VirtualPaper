# Testing and local acceptance

[English](README.md) | [简体中文](README.zh-CN.md)

The local test chain combines MSTest project tests, real installer acceptance and WinUI interaction tests. Start with `Run-Tests.ps1` for the complete flow; the focused runners below reuse its individual stages.

`VirtualPaper.UIOperationTest` is a standalone, unpackaged WinUI executable that also serves as the test host. Component cases load production pages through project references. Application cases start an isolated, real Core process, which launches the real UI. Neither uses Sandbox or connects to a user's running VirtualPaper instance.

## Layout

```text
test/
  Run-Tests.ps1                      Complete local Release/installed acceptance
  Run-ProjectTests.ps1               One prebuilt project test job
  Run-InstallerTests.ps1             Real package install/open/UI/uninstall
  Run-UIOperationTests.ps1
  Summarize-TestResults.ps1          Validate raw reports and coverage
  VirtualPaper.InstallerTest/        Installation wizard and first-run probes
  VirtualPaper.UIOperationTest/
    App.xaml / App.xaml.cs          UI thread, case registration, JSON results
    Cases/
      Components/                   Production-page interaction tests
      Application/                  Real Core + UI tests, grouped by module
    Infrastructure/
      UiTestContext.cs              Page lifecycle, waits, UI Automation, temporary files
      AppProcessSession.cs          Processes, RPC readiness, UI restarts, shutdown
      ApplicationUi.cs              Cross-process control discovery and interaction
```

## Running the tests

Run commands from the repository root. AI agents should start with [AGENTS.md](../AGENTS.md) and the [VirtualPaper testing skill](../.agents/skills/virtualpaper-testing/SKILL.md). The skill explains test selection, process isolation, report interpretation and workflow gates.

| Goal | Entry point | Scope |
| --- | --- | --- |
| Complete local acceptance | `test/Run-Tests.ps1` | Fresh Release rebuild, parallel project tests, ISS and installed extended UI acceptance |
| Focused project regression | `test/Run-ProjectTests.ps1 -JobId <id>` | One project using existing Release output; no build |
| Real UI module or suite | `test/Run-UIOperationTests.ps1` | Builds the solution unless `-NoBuild`; runs component/application cases |
| Existing installer | `test/Run-InstallerTests.ps1 -InstallerPath <setup.exe>` | Real installation, first launch and uninstall; add `-RunUIOperationTests` for extended UI |
| Workflow/script contracts | `.github/scripts/validate-test-workflow.ps1` | Isolated checks; no install or real UI |

For the complete local acceptance used before merging, start PowerShell 7.2 or later **as administrator** and run:

```powershell
./test/Run-Tests.ps1
# Include the optional ML project integration tests as well:
./test/Run-Tests.ps1 -IncludeMLIntegration
```

This entry point rebuilds the full solution in **Release**, runs the same eight project test jobs in parallel as CI, compiles `InnoSetup/setup.iss`, installs the resulting package, checks first launch and normal exit, runs the **extended** real UI suite against the installed Core/UI, then uninstalls. A failed phase blocks subsequent phases; uninstall still runs after installation, startup or UI failures. Each invocation writes fresh reports below `TestResults/local-acceptance-*`. Visual Studio MSBuild, .NET 8, Node.js and Inno Setup 6.7.1 are required; use `-InnoSetupCompiler <ISCC.exe>` for a nonstandard compiler location.

It refuses an existing Virtual Paper installation or Start menu shortcut. Remove a daily installation explicitly while preserving its data, or use a clean Windows test machine. Installation and uninstallation change the OS uninstall registration and shortcuts; UI cases still exclude startup registration, session locking and other persistent system-setting writes. A missing desktop produces the same explicit UI skip as Pre-merge Check; it is not full UI acceptance.

`-IncludeMLIntegration` selects the regular ML project's optional integration category. It does not control the installed UI suite: complete local acceptance always requests **Extended**, including real inference and depth rendering. That suite requires its model files even when the switch is omitted.

The complete flow is:

```mermaid
flowchart LR
    A[Workflow contracts] --> B[Release rebuild]
    B --> C[8 parallel project jobs]
    C --> D[ISS package]
    D --> E[Install and first launch]
    E --> F[Extended installed UI]
    F --> G[Uninstall and summary]
```

Failures block later phases; once installation is attempted, its cleanup path still runs. Local execution uses the working tree; Pre-merge Check uses the PR merge snapshot, while Pre-release tests the versioned release build and requires all selected tests to run without skips. Local tests do not set GitHub PR status or synchronize versions. See the [workflow guide](../.github/workflows/README.md).

Use `-ResultsRoot <new-directory>` to choose the complete run's report root. It contains `build.log`, `workflow-contracts.log`, eight `test-*.log` files, `installer-build.log`, `installed-acceptance.log`, the fresh `installer-package`, and raw `test-results-*` reports. Failed reports are retained. To validate a completed run with the release no-skip gate:

```powershell
./test/Summarize-TestResults.ps1 -Root '<result-directory>' -RequireFullAcceptance
```

This checks existing evidence; it does not run omitted tests or prepare a release. A desktop skip or skipped selected project test fails this stricter gate.

### Focused project tests

After building the complete Release solution with Visual Studio MSBuild, use a fresh report directory:

```powershell
msbuild src/VirtualPaper.sln /t:Build /p:Configuration=Release '/p:Platform=Any CPU' /restore /m
./test/Run-ProjectTests.ps1 -JobId test-core -ResultsDirectory "TestResults/core-review-$([guid]::NewGuid().ToString('N'))"
```

Use Developer PowerShell so `msbuild` is available. The solution's platform mapping must be preserved; standalone host builds with a different architecture can leave mismatched dependency copies.

| Job ID | Project |
| --- | --- |
| `test-artifacts` | `VirtualPaper.ReleaseBuildData.Test` |
| `test-core` | `VirtualPaper.Core.Test` |
| `test-ui-logic` | `VirtualPaper.UI.Test` |
| `test-images` | `StaticImg.Test` |
| `test-web-backdrop` | `WebBackdrop.Test` |
| `test-ui-components` | `VirtualPaper.UIComponent.Test` |
| `test-ml` | `VirtualPaper.ML.Test` |
| `test-shader` | `VirtualPaper.Shader.Test` |

`Get-ProjectTestSpecs` in `TestReportSupport.ps1` is the authoritative job/report mapping. CI and complete local acceptance also run `node .github/scripts/test-atmosphere.cjs` and `node .github/scripts/test-depth-player.cjs` alongside UI logic tests. A focused project run does not invoke those two script checks.

### Focused real UI tests

The focused UI runner below is for module development; it does not replace installed-package acceptance:

Use PowerShell 7.2 or later on a Windows desktop with a graphical session:

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release
# Full regression: also run real inference, repeated UI lifecycles and 3D rendering.
./test/Run-UIOperationTests.ps1 -Configuration Release -Suite Extended
# Run one application module. The default suite excludes opt-in integration cases.
./test/Run-UIOperationTests.ps1 -Configuration Release -Module WallpaperLibrary
# Requires an existing Real-ESRGAN model in the installation; no automatic download.
./test/Run-UIOperationTests.ps1 -Configuration Release -Module ModelInference -TimeoutSeconds 900
```

The runner builds the full solution with Visual Studio MSBuild before starting the tests. Use `-NoBuild` when the binaries are ready. Core is loaded from `src/VirtualPaper/bin/<Configuration>/net8.0-windows10.0.19041.0` by default. Use `-CoreExe` to select another complete build, including its production UI plugin.

Use `-ReportPath` to specify a new JSON report path; the default is in the system temporary directory. Existing report or artifact paths are rejected to prevent stale results from being reused. The runner checks the exit code, expected case count, and report completeness. Failures throw an error; a timeout terminates the host and child processes started by this run. **Do not run this WinUI executable with `dotnet test`.**

The default overall timeout is 180 seconds, or 900 seconds for `-Suite Extended`. Set `-TimeoutSeconds` to a value from 30 to 900 as needed. The report's adjacent `.artifacts` directory retains isolated settings and Core/UI logs; temporary runtime directories are cleaned up.

The standard suite contains 17 cases. The extended suite contains those cases plus real model inference and queue cancellation, 12 generations of UI open/close and screen-saver page notifications, and depth inference with rendered-frame pause/resume checks. Its Real-ESRGAN and Depth Anything models must already be installed; missing models fail the extended acceptance rather than silently reducing coverage. CI runs this extended suite. Real idle screen-saver testing is still opt-in; session locking and registry modifications remain excluded.

### Machines without a usable desktop

Before building or launching test processes, a read-only preflight checks the Windows interactive session, access to the current input desktop, physical or virtual display bounds, and the Explorer desktop required for wallpaper acceptance.

An unsuitable environment produces a report with `Status: Skipped`, `Passed: false`, `ExpectedCount: 0`, and a reason, then exits with code 0. Reasons include `WindowsRequired`, `NoInteractiveDesktop`, `NoDisplay`, and `NoDesktopShell`. Details are saved to `.artifacts/environment.json`.

Only real UI acceptance is skipped; regular tests can still run. The CI summary explicitly reports the skip rather than claiming full acceptance. Preflight errors, missing reports, and failures or timeouts after startup remain failures. Detection does not lock or unlock the session, switch input desktops, modify the registry, or create virtual displays.

## Default coverage

### Live language changes

`Localization.LiveText.RefreshAndPreserveState` switches between Chinese and English on production controls. It checks brush labels, preset style names, unnamed-style prompts, and queued task labels, while preserving brush settings, selected styles, input paths, and custom names.

### Component effects

`GeneralEffect.RImage.EditRestoreSaveAndReopen` and `GeneralEffect.RImage3D.EditRestoreSaveAndReopen` cover ordinary and depth images:

1. Navigate to the production `GeneralEffect` page in a real WinUI window and check the appropriate effect groups.
2. Expand options with UI Automation ExpandCollapse, change loaded sliders with RangeValue, and check event delivery.
3. Invoke Restore, repeating five cycles to check values, control counts, and page attachment.
4. Save and verify the actual JSON and apply callback. Recreate the page, expand the options, and check that saved values are displayed.

### Navigation settings

`Application.NavigationSettings.SaveAndReopen`:

1. Start real `VirtualPaper.exe` and wait for readiness through a settings RPC with a timeout.
2. Use production ShowUI RPC to launch `VirtualPaper.UI.exe`, accepting only a direct child of this Core.
3. Navigate through cross-process UI Automation and disable navigation animations without calling the ViewModel or SetSettings directly.
4. Independently check the backend RPC and persisted JSON.
5. Close UI normally, ask Core to start another UI process, and check that the switch remains off.
6. Close UI and Core normally and check exit codes. Record PIDs, persisted values, and shutdown results.

Reopening waits for the real window to become ready. If the OS reports process exit before Core finishes its exit callback, the runner makes bounded retries of the idempotent ShowUI command. `ShowUiRequests` records these attempts; an RPC response alone does not mean the window is ready.

### Performance settings

`Application.PerformanceSettings.SaveAndReopen` checks seven settings: playback policies for fullscreen, focus, battery, power saving, and Remote Desktop; multi-monitor detection; and desktop-only audio.

Each real control is changed, confirmed through Core RPC and persisted JSON, and checked again after reopening UI. This tests settings persistence, not simulated battery/RDP states or actual wallpaper pause/mute behavior.

### Wallpaper library

`Application.WallpaperLibrary.ImportApplyCloseDelete`:

1. Import an isolated PNG through the actual Add button and system picker. Check metadata, thumbnail, and the source hash.
2. Search for a nonmatch and clear the search to restore the entry. An ordinary image may initially have an empty title, so this does not validate nonempty-title matching.
3. Focus the card, open its production menu with Shift+F10, cancel deletion and check that files remain, then apply it as an ordinary image.
4. Check layout JSON, the PlayerWeb PID directly created by Core, and the player's actual WorkerW parent window.
5. Reopen UI and check that the entry persists. Close the wallpaper through display controls and check the empty layout and player exit.
6. Confirm deletion, check removal from disk and the list, and reopen UI to verify the empty library. The source PNG must remain unchanged.

Card containers expose an AutomationId based on wallpaper UID. Keyboard and mouse menus use the same production handler. Native input is restricted to owned test windows; any temporary topmost state is restored. Other desktop applications are not closed.

The real picker must submit successfully; storage services are not replaced. Menu lookup failures retain an owned-window screenshot in the artifacts. Cleanup waits for WebView2 to release delayed cache handles. Desktop mounting and shutdown assertions alone do not establish 3D rendering quality or pause correctness; the Rendering module covers those separately.

### Screen saver settings

`Application.ScreenSaver.SettingsAndWhitelist`:

1. Enable the screen saver and change its wait time and effect through the real page, then independently check Core RPC and JSON.
2. Select system `notepad.exe` through the production asynchronous `IStoragePicker`. Register it in the whitelist without launching it.
3. Check its name, full path, and saved icon; test matching and nonmatching searches; reopen UI and check persistence.
4. Remove the entry and icon, disable the screen saver, and reopen UI to verify the disabled state and empty list.

Enabling waits for settings to finish saving before sending the start RPC. Whitelist paths round-trip through gRPC, and the notification pipe carries the isolated session suffix.

Picker submission uses an asynchronous click on a verified native Button within the owned picker, identified by class and native ID. It does not send a global Enter key; this also avoids synchronous UIA/COM errors during Save As validation. The case uses a long idle timeout with locking disabled. It does not validate real idle activation, screen saver process startup, or system locking.

### Fit to screen

`Application.ScreenFit.PreviewCancelApplyAndReopen`:

1. Pick a portrait image through the system picker, wait for the sample preview to disappear, switch contain/cover, and check the visible-area description.
2. Cancel and check that no wallpaper, layout, or player was created. Reopening must require a new source selection.
3. Apply with enhancement off and contain selected. Check the source hash, target monitor, and Scaling in three effect JSON files.
4. Check PlayerWeb mounting under WorkerW, then reopen UI and verify the library entry, current preview, and persisted settings.
5. Close the player and delete the fixture through production UI. Check the unchanged source and normal Core/UI shutdown.

This case does not run super-resolution or validate inference, preview pixel quality, monitor switching, or actual cover-mode rendering.

### Wallpaper effects

`Application.WallpaperEffects.EditRestoreSaveAndReopen` uses real Core, UI, player, and effect services:

1. Import and apply an image, then open its production adjustment window from display controls.
2. Change atmosphere and intensity, restore the saved values, then select Snow with intensity 71.
3. Check that applied configuration is unchanged before Save; after Save, verify effect, style, and intensity on disk while the default template remains unchanged.
4. Reopen UI and the adjustment window, check all three controls, then close the player and delete the fixture.

Changes use production RPC/IPC while the player remains running. The case does not inspect renderer state or pixels and does not replace particle-quality, depth-model, mouse-parallax, or pause/resume checks.

### General settings

`Application.GeneralSettings.SaveLanguageThemeAndReopen`:

1. Switch light/dark theme and change backdrop, startup preference, and language. Check each change through Core RPC and JSON.
2. Navigate among General, System, and Other, then verify retained settings and the About image's accessible name.
3. After a backdrop change, observe Core automatically relaunch UI. Switch Chinese/English in the same UI process and verify that tab labels and backdrop choices update immediately without restarting.
4. Close and reopen UI, check Chinese labels, backdrop options, language, and startup preference, then restore English and disable the startup preference.

Explicit test sessions skip OS startup registration. This case does not validate real registration, backdrop pixels, About image decoding, or update downloads.

### Main window and tray themes

`Application.ThemeSync.MainWindowAndTray` cycles Light → Dark → Auto → Light → Dark through the real theme button. It checks RPC and JSON persistence, captures the real main window and the owned Core tray menu, and verifies their rendered background luminance. It then reopens UI and checks Dark again. Auto is checked against the current system preference; the test never changes the system theme or registry.

The same case opens and cancels a new AI task in both Light and Dark, checks the button's accessible name, keyboard focus and hit target, and retains screenshots. Test sessions label their tray icon with the session ID so acceptance cannot click another running instance's icon.

### Log export

`Application.SystemSettings.ExportLogsAndCancel` cancels the production save picker, then exports a ZIP to the case's temporary directory. It waits until the archive is fully readable and checks settings against the session JSON, the presence of layout data, nonempty Core/UI logs, and safe archive entry paths. It then reopens UI and checks normal shutdown.

Only isolated session diagnostics are exported, not production user logs. The backend debug window is not opened.

### Static editor

`Application.StaticEditor.RotateCancelSaveReopenAndExport`:

1. Open an isolated PNG through the production entry point and picker. Rotate the current default 1920×1080 canvas to 1080×1920.
2. Cancel the unsaved-close prompt and verify UI remains open. Cancel one design save, then save a `.vpd`.
3. Read dimensions and layer count using the production file-header reader. Reopen UI, load the `.vpd`, and check the canvas.
4. Export PNG through the production menu; check dimensions, nonempty image content, source hash, and normal shutdown.

This uses real Win2D and shader resources. Brushes, layer renaming, cross-DPI behavior, and pixel-perfect output are outside its coverage.

### Web editor

`Application.WebEditor.CreateFilesSaveReopenAndExport`:

1. Create an isolated project from a production template, create a folder, cancel file creation once, then create and delete files.
2. Open the entry file through the tree and wait for Monaco's current-file state. Verify native focus belongs to the browser before sending real keyboard input.
3. Save through the production menu and independently read the edited marker from disk; a cleared dirty indicator is insufficient.
4. Reopen UI and load the `.vpw` through the system picker. Check the tree and entry file, export the complete ZIP, and verify edited content, manifest, and deleted files.

Context menus and keyboard input operate only on owned windows with foreground and obstruction checks. No DOM injection, test-only save endpoint, or substituted service is used. Preview quality, Save As, every editor command, OCR, and pixel-level rendering are outside this case.

### Exit with unsaved editors

`Application.EditorExit.ImageCancelSaveAndExit` and `Application.EditorExit.WebCancelSaveAndDiscard` leave real image/Web editors dirty and cancel a window-close prompt. They then request Core shutdown through the production RPC (image) or the actual tray Exit action (Web), cancel the real unsaved dialog, and confirm UI and Core remain usable.

The image case also cancels the save picker, then requests exit again and saves a `.vpd`, checking its edited canvas dimensions. The Web case opens the session's actual tray menu, chooses Exit, discards the edit and checks the entry file remains unchanged. Both require normal UI/Core exit codes, unchanged source files, and no ERROR logs. Cancelled Core requests must finish before the next request. Dialog buttons are located inside the ContentDialog to avoid confusing Cancel with a TabView close button.

### Intelligent task configuration

`Application.Intelligent.ConfigureCancelAndReopen`:

1. Cancel the production image picker and check that submission remains disabled.
2. Select a 256×144 image, switch between 2×, 4×, and restoration, and check the 512×288, 1024×576, and original-size descriptions.
3. Cancel and reopen to verify source and scale reset. For style transfer, select only a style image and check that submission stays disabled until a source is added.
4. Repeat cancellation and reopening, checking usability, unchanged source hashes, and normal shutdown.

This does not submit inference tasks or download models. The report explicitly records `ModelInferenceAndQueueTested=false`. Inference, queueing, running-task cancellation, and result import/export require the opt-in case below.

## Opt-in integration cases

These cases are excluded from the default suite. Select a module explicitly, or use `-Suite Extended` to include model inference, screen saver lifecycle, and rendering. Real idle screen saver testing requires an explicit module selection.

### Model inference and queue

`Application.ModelInference.SuperResolutionQueueCancelSaveAndImport`:

1. Submit three tasks through production UI using the installed Real-ESRGAN ONNX model.
2. Verify the third task is queued and the second running, cancel both, and let the first complete real inference.
3. Export PNG through the system save picker, check 2× dimensions, import through the production button, and compare exported/imported hashes.
4. Delete the completed task, reopen UI, and verify the library entry, unchanged source, normal shutdown, and absence of ERROR logs.

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release -Module ModelInference -TimeoutSeconds 900
```

A missing model fails the case; there is no automatic download or silent skip. This verifies CPU ONNX inference and basic queue behavior, not subjective quality, every model, or long-running stress.

### Screen saver lifecycle

`Application.ScreenSaverLifecycle.NavigateNotifyCloseAndReopen` starts and normally closes 12 UI generations under one real Core. Each visits the screen saver page twice and receives settings notifications through the real session pipe, with receipt confirmed in UI logs. It checks listener recovery and shutdown, not real idle or locking.

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release -Module ScreenSaverLifecycle -TimeoutSeconds 300
```

If UI shutdown exceeds 10 seconds, visible controls and an owned-window screenshot are retained. The case still fails; the timeout is not extended to hide the issue.

### Rendering

`Application.Rendering.DepthInferenceDrawPauseAndResume` uses the real picker, 3D type selection, and installed Depth Anything V2. It checks the depth cache, enables automatic camera movement, and captures WebView pixels through the production screenshot RPC. Assertions cover fixture colors, moving-frame differences, paused stability, and resumed movement.

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release -Module Rendering -TimeoutSeconds 300
```

Captures come only from the owned player and are retained under the report's `logs/UIOperation/rendering`. Missing models fail without downloading. This checks basic rendering correctness, not subjective quality, multiple GPUs/monitors, or pause support in third-party Web wallpapers.

### Real idle screen saver

`Application.ScreenSaverIdle.RealIdleInputWakeAndRepeat` waits for one minute of actual idle time, checks screen saver loading, wakes it with mouse input, verifies normal exit, and repeats the cycle.

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release -Module ScreenSaverIdle -TimeoutSeconds 300
```

Always-move input forwarding is disabled; screen saver activity notifications must still work. The case uses the screen saver's visible HWND, accounting for its hidden WPF owner. Each round waits at most two minutes. Real keyboard or mouse activity resets the timer; observed OS idle seconds are reported to identify interference. Only non-locking screen savers run. Locking, startup registration, and persistent system-setting writes are skipped.

## Isolation, results, and limits

`Passed` means the case's stated interaction and persistence assertions passed, not that the whole application is healthy. An environment skip is not a pass. `Evidence.RuntimeErrors` retains ERROR logs separately, and the runner warns about them even when the exit code is 0. Failed cases snapshot Core/UI logs and configuration before later process generations can rotate the logs.

Child-process-only environment variables carry a test session GUID and data root. Common reads them during initialization to isolate settings, logs, Core/UI singletons, pipes, and shared file locks. Process tests **do not enable `Constants.IsTestMode`**: shader paths, messages, and desktop mounting checks retain production behavior. Regular unit tests can still use `IsTestMode`; data isolation is independent of that flag.

Explicit sessions skip startup registration, startup notifications, and pending plugin updates, while settings still save through production code. Real locking and registry-modification test cases are **skipped** under the project test policy, are not registered, and do not request permission. Process cases keep screen saver locking disabled and skip wallpaper refresh with `SPIF_UPDATEINIFILE`, which would persist system settings. `SkippedSystemOperations` records these limits; they are not counted as passes. Without the environment variables, application behavior is unchanged. Tests do not read or write the user's wallpaper library or production configuration.

Component cases run serially on the UI thread. Cross-process UI Automation runs on a separate MTA thread; see [Microsoft's UI Automation threading guidance](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading). Component cases use recording implementations for effect services and apply callbacks. Application cases use the real backend. Library, screen-fit, and effect cases start a real player and verify desktop mounting within their stated limits.

The default suite does not validate WebView rendering quality, multi-monitor layouts, video playback policies, or AI inference. The seven regular test projects remain in their existing locations. `Core.Test/T_Workflows` continues to cover file, service, and gRPC integration rather than moving into the UI host.

## CI and installer acceptance

Pre-merge Check runs **build once → seven parallel regular test projects → compile ISS → install → first launch → installed UI tests → uninstall → summary**. Artifact integrity checks run alongside regular tests; Web player script checks run with the UI unit-test job. A failed prerequisite prevents packaging/installation. One acceptance job owns the installation through cleanup; there is no second installation for UI tests.

Pre-merge Check compiles `InnoSetup/setup.iss` from the shared build and retains the installer as `test-installer-package`. It uses `Run-InstallerTests.ps1` to install that package, launch Core from the installed location, let first-run behavior open UI, check that the window remains responsive for three seconds, then close UI and shut down Core through the production RPC. Next, the prebuilt UI host runs the extended suite with `-CoreExe <installation>/VirtualPaper.exe`; application cases start the installed Core/UI/player executables. The host stays outside the installation, and nothing is rebuilt. Forced cleanup is failure recovery, not a successful shutdown.

Pre-release repeats these checks against its newly versioned build and actual release installer. It reuses the same project action, includes ML integration and Web player script tests, and runs the same extended installed-UI suite. `Summarize-TestResults.ps1 -RequireFullAcceptance` rejects skipped project tests or desktop-unavailable startup/UI skips as well as failures and missing reports. Only successful full acceptance permits version synchronization and draft creation, including for plugin releases (which skip product-version synchronization).

Each installer run uses a unique temporary installation and isolated application data. Startup failure skips UI tests. Install, startup, or UI failure still triggers uninstall; uninstall residue also fails. Combined reports contain Install, Open, UIOperation, and Uninstall results, alongside the detailed UI report. Standalone installation checks retain the three-stage report.

With an interactive desktop, the test operates the real English installation wizard: accept the license, proceed, install, and finish. Uninstall confirms removal, preserves user data, and completes. Only the installer and its child-process windows are operated; unknown dialogs or timeouts fail. Without a desktop, install and uninstall run silently, and both Open and UIOperation are explicitly `Skipped`, with matching desktop evidence in the UI report.

Both modes check required Core/UI files, package/program version agreement, Start menu shortcuts, uninstall registration, and the recorded installer language. Desktop runs also verify that a fresh application's UI, Core settings, and persisted settings use that language. Existing user settings retain their language during upgrades. After uninstall, the runner checks file, process, shortcut, and registration residue. Reports and installer logs are retained; desktop runs also retain wizard traces.

**Installer acceptance performs actual system installation and uninstallation.** Use a clean CI machine or an explicitly prepared local administrator session. It refuses an existing installation with the same AppId or an existing Start menu shortcut. This is separate from the UI host's registry-free cases and must not be run where installation-related registry changes are prohibited.

```powershell
# Disposable Windows installation-test machine only:
./test/Run-InstallerTests.ps1 -InstallerPath <installer.exe> -ReportPath TestResults/Installer/installer-results.json
# Use the Chinese wizard and verify that the application starts in Chinese:
./test/Run-InstallerTests.ps1 -InstallerPath <installer.exe> -InstallerLanguage chinese -ReportPath TestResults/InstallerChinese/installer-results.json
# Full acceptance, using a prebuilt UI host; always uninstalls after the UI stage:
./test/Run-InstallerTests.ps1 -InstallerPath <installer.exe> -NoBuild -RunUIOperationTests `
  -UIHostExe test/VirtualPaper.UIOperationTest/bin/x64/Release/net8.0-windows10.0.19041.0/VirtualPaper.UIOperationTest.exe
# Contract checks only: no installation, UI startup, or registry writes.
./test/Test-InstallerTestSupport.ps1
# On a desktop, validate UIA, child-process handoff, and timeout recovery with a temporary wizard fixture that does not modify system settings.
./test/Test-InstallerWizard.ps1
./.github/scripts/validate-test-workflow.ps1
```

A WinUI failure or missing report fails the final CI check. Pre-merge Check may accept an explicit environment skip from preflight, with `[SKIPPED]` in the final status; Pre-release rejects it and requires an interactive desktop. Before WinUI starts, regular TRX reports are also checked for unhandled exceptions so passing assertions cannot hide a crashed testhost.

## Adding coverage

Implement new cases under `Cases` and register them in `App.xaml.cs`; the runner and CI do not maintain hardcoded case names or counts.

- Commit shared test infrastructure and the smallest real scenario separately.
- Add one module per subsequent commit, including any necessary shared helpers.
- Verify real interaction, backend persistence, reopened state, and process cleanup before moving to the next module.
- Document uncovered behavior and known errors in reports. Keep cross-module fixes in separate commits.
- Do not commit reports, runtime logs, or unrelated local changes with a feature.
