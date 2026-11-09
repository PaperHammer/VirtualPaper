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
| Complete local acceptance | `test/Run-Tests.ps1` | Fresh Release rebuild, parallel project tests, ISS/installer smoke and independent extended UI |
| Focused project regression | `test/Run-ProjectTests.ps1 -JobId <id>` | One project using existing Release output; no build |
| Real UI module or suite | `test/Run-UIOperationTests.ps1` | Builds the solution unless `-NoBuild`; runs component/application cases |
| Existing installer | `test/Run-InstallerTests.ps1 -InstallerPath <setup.exe>` | Required installer smoke: real install/reinstall, startup/normal exit, language/data retention and uninstall |
| Workflow/script contracts | `.github/scripts/validate-test-workflow.ps1` | Isolated checks; no install or real UI |

For the complete local acceptance used before merging, start PowerShell 7.2 or later **as administrator** and run:

```powershell
./test/Run-Tests.ps1
# Include the optional ML project integration tests as well:
./test/Run-Tests.ps1 -IncludeMLIntegration
```

This entry point rebuilds the full solution in **Release**, runs eight project jobs in parallel, compiles ISS, then runs mandatory installer smoke: English install/first launch → Chinese same-package reinstall → preserved/fresh-profile startup checks → uninstall. After smoke completes, an independent **Extended** UI suite runs against the Release output with its own Core/UI sessions. A failed local phase blocks subsequent phases; smoke always attempts uninstall after installation. Reports use a fresh `TestResults/local-acceptance-*` directory. Visual Studio MSBuild, .NET 8, Node.js and Inno Setup 6.7.1 are required; use `-InnoSetupCompiler <ISCC.exe>` for a nonstandard compiler location.

It refuses an existing Virtual Paper installation or Start menu shortcut. Remove a daily installation explicitly while preserving its data, or use a clean Windows test machine. Installation and uninstallation change the OS uninstall registration and shortcuts; UI cases still exclude startup registration, session locking and other persistent system-setting writes. Without a desktop, independent UI is explicitly skipped, but smoke cannot pass because its required startup/profile checks did not run.

`-IncludeMLIntegration` selects the regular ML project's optional integration category. It does not control the independent UI suite: complete local acceptance always requests **Extended**, including real inference and depth rendering. That suite requires its model files even when the switch is omitted.

The complete flow is:

```mermaid
flowchart LR
    A[Workflow contracts] --> B[Release rebuild]
    B --> C[8 parallel project jobs]
    C --> D[ISS package]
    D --> E[English install and first launch]
    E --> R[Chinese reinstall and profile checks]
    R --> U[Uninstall - smoke complete]
    U --> F[Independent extended UI suite]
    F --> G[Summary]
```

Failures block later local phases; installation cleanup still runs. Local execution uses the working tree; CI uses the PR snapshot or versioned release build. Installer smoke allows 240 seconds per stage by default (the wizard uses 230 seconds); full-package extraction is bounded separately from the application startup checks. Both CI workflows tolerate only the independent UI job through `continue-on-error` and summary `-AllowUIFailure`; required project/installer stages remain blocking. Local defaults still reject UI failures. Local testing does not set PR status or synchronize versions. See the [workflow guide](../.github/workflows/README.md).

Installer runtime profiles use short, isolated temporary paths to stay within Inno Setup 6 path limits. They are copied to the installer report's `.artifacts/profiles` directory after acceptance, then removed from the temporary location.

Use `-ResultsRoot <new-directory>` to choose the complete run's report root. It contains `build.log`, `workflow-contracts.log`, eight `test-*.log` files, `installer-build.log`, `installer-smoke.log`, `ui-acceptance.log`, the fresh `installer-package`, and raw `test-results-*` reports. Failed reports are retained. To validate a completed run with the release no-skip gate:

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
# Select one exact application case without repeating its entire module:
./test/Run-UIOperationTests.ps1 -Configuration Release -NoBuild -Module WallpaperLibrary.ImportMultipleFormatsAndReopen
# Requires an existing Real-ESRGAN model in the installation; no automatic download.
./test/Run-UIOperationTests.ps1 -Configuration Release -Module ModelInference -TimeoutSeconds 900
```

The runner builds the full solution with Visual Studio MSBuild before starting the tests. Use `-NoBuild` when the binaries are ready. Core is loaded from `src/VirtualPaper/bin/<Configuration>/net8.0-windows10.0.19041.0` by default. Use `-CoreExe` to select another complete build, including its production UI plugin.

Use `-ReportPath` to specify a new JSON report path; the default is in the system temporary directory. Existing report or artifact paths are rejected to prevent stale results from being reused. The runner checks the exit code, expected case count, and report completeness. Failures throw an error; a timeout terminates the host and child processes started by this run. **Do not run this WinUI executable with `dotnet test`.**

The default overall timeout is 180 seconds, or 900 seconds for `-Suite Extended`. Set `-TimeoutSeconds` to a value from 30 to 900 as needed. The report's adjacent `.artifacts` directory retains isolated settings and Core/UI logs; temporary runtime directories are cleaned up.

The standard suite contains 19 cases; prepared 3D playback needs the bundled Depth Anything model to validate its cache key, without running inference. The extended suite contains those cases plus real model inference and queue cancellation, 12 generations of UI open/close and screen-saver page notifications, and depth inference with rendered-frame pause/resume checks. Its Real-ESRGAN and Depth Anything models must already be installed; missing models fail the extended acceptance rather than silently reducing coverage. CI runs this extended suite. Real idle screen-saver testing is still opt-in; session locking and registry modifications remain excluded.

### Machines without a usable desktop

Before building or launching test processes, a read-only preflight checks the Windows interactive session, access to the current input desktop, physical or virtual display bounds, and the Explorer desktop required for wallpaper acceptance.

An unsuitable environment produces a report with `Status: Skipped`, `Passed: false`, `ExpectedCount: 0`, and a reason, then exits with code 0. Reasons include `WindowsRequired`, `NoInteractiveDesktop`, `NoDisplay`, and `NoDesktopShell`. Details are saved to `.artifacts/environment.json`.

Independent UI can be skipped when no suitable desktop is available, and summaries report it explicitly. Local UI failures/timeouts/missing reports still fail by default. CI only tolerates the separate UI job; installer smoke must pass every stage, including startup/normal exit, in both workflows. Desktop detection does not modify the session, registry or display configuration.

## Default coverage

### Live language changes

Source translations live only in `VirtualPaper.UIComponent/Strings`; the normal solution build generates XAML property resources from `Localization/XamlResources.xml`. Artifact tests use an embedded mapping snapshot to validate the packaged generated key set and matching source values without relying on resource comments. See [localization resources](../src/VirtualPaper.UIComponent/Localization/README.md) for editing and standalone generation/check commands.

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
   Reopen a format-18 absolute-path fixture, invoke the real context-menu Update action, and verify format 19, relative stored paths, the same UID and unchanged source pixels.
2. Search for a nonmatch and clear the search to restore the entry. An ordinary image may initially have an empty title, so this does not validate nonempty-title matching.
3. Focus the card, open its production menu with Shift+F10, cancel deletion and check that files remain, then apply it as an ordinary image.
4. Check layout JSON, the PlayerWeb PID directly created by Core, and the player's actual WorkerW parent window.
5. Reopen UI and check that the entry persists. Close the wallpaper through display controls and check the empty layout and player exit.
6. Confirm deletion, check removal from disk and the list, and reopen UI to verify the empty library. The source PNG must remain unchanged.

Card containers expose an AutomationId based on wallpaper UID. Keyboard and mouse menus use the same production handler. Native input is restricted to owned test windows; any temporary topmost state is restored. Other desktop applications are not closed.

The real picker must submit successfully; storage services are not replaced. Menu lookup failures retain an owned-window screenshot in the artifacts. Cleanup waits for WebView2 to release delayed cache handles. Desktop mounting and shutdown assertions alone do not establish 3D rendering quality or pause correctness; the Rendering module covers those separately.

`Application.WallpaperLibrary.ImportMultipleFormatsAndReopen` selects the collection's image, GIF, video and Web-archive inputs in **one real native picker submission**, using actual Shell row multi-selection. It verifies every imported file/thumbnail, unique identities, all media types, unchanged input hashes, and every card after reopening UI.

`Application.WallpaperLibrary.BuiltInsApplyRenderAndClose` arranges the prepared collection in an isolated built-in library before Core starts. It discovers every card through the real library, applies each item, checks WorkerW attachment and a nonblank player screenshot, and requires changing rendered pixels for GIF/video/Web. The display close action must clear the layout and stop its owned player process; the player implementation intentionally terminates that process. It also applies a prepared image as 3D and checks that its depth cache is preserved. These arranged files do not prove ISS deployment; the independent mandatory smoke checks that separately.

The original single-image import/update/delete case and real depth-inference/pause/resume case remain: prepared content cannot substitute for those distinct paths. Playback checks reuse the existing picker, wallpaper and screenshot helpers.

Library organization logic is covered separately by `WallpaperOrganizationTests`, `WpSettingsViewModelTests` and `LibraryContentsViewModelTests`: full-index search across title/description/author/source and personal tags, combined favorites/type/tag filters, deterministic sorting before paging, skipped missing metadata and stale-query rejection. Tag filtering matches typed substrings after a short input delay (case-insensitive, ignoring surrounding whitespace), with suggestions from existing source and personal tags; selecting a suggestion is optional. Tests cover partial input, empty/no matches and inventory refresh without replacing the input. Preferences live in `wallpaper_library.json` under the application data directory, keyed by wallpaper UID. Tests check batch tag updates, concurrent writes, failed-save rollback, corrupt-file preservation, restart, built-in metadata refresh and library relocation without changing wallpaper metadata. Multi-selection operates on loaded cards. These unit tests do not establish the new toolbar, favorite-button or tag-dialog interaction; real UI acceptance requires explicit permission.

Single-wallpaper editing uses an **Edit info** dialog for title, description, author and personal tags. Included wallpaper tags are read-only; favorites remain on cards and menus and are never overwritten by saving the form. Multi-selection retains batch tag operations. Context menus explicitly capture their target(s) before opening. Favorites actions are available only when they can change a target, using the same persisted state as card markers. Logic tests cover selected/unselected context targets, uniform/mixed favorites, saved metadata becoming searchable, preservation of included tags and current favorites, unchanged metadata for personal-tag-only edits, metadata-write failure, and retry after a preference-write failure. Metadata and preferences are separate saves: if the second fails, the first remains saved and searchable. Mouse/keyboard menu opening, dialog layout, cancellation and rendered star updates still require real UI acceptance.

Author and personal-tag fields add removable items with Enter. `WallpaperInfoTokensTests` covers existing semicolon lists, empty/duplicate input, removal, pasted tags, commas within author names, and pending input included when saving/retrying. The metadata save test also checks multiple persisted authors and author search. The editor uses the dialog's existing scroll area instead of nesting another scroll viewer; alignment, wrapping chips and Enter preventing dialog submission still need real UI verification.

Library input queries wait 180 ms to coalesce typing; superseded queries leave the current cards intact and never query the index. Paging reuses the current filtered/sorted index snapshot. Batch imports publish successful items together and refresh once, including when a later import fails or is cancelled. Data refreshes preserve the loaded item count without requesting a scroll reset; changed filters start from the first page. Logic tests cover batch publication, empty batches, retained paging and a pending filter during import. Metadata changes, library rebuilds and saved personal preferences invalidate the affected query/tag caches; logic tests cover these paths, including relocation. The GridView owns its scroll viewport so cards can be recycled. Card actions reveal only after 120 ms of hover; leaving, scrolling, recycling or entering selection mode cancels the pending reveal and stops/hides an active animation. Hover refreshes only the affected card. Viewport virtualization, automatic paging and hover timing still require real UI verification; logic tests do not establish frame rate or memory use.

Preview resource cleanup is also covered by headless logic checks: `WebViewScriptExecutorTests` verifies ordered events, per-key coalescing of burst state updates, idempotent disposal, and termination of both worker loops even when dispatched callbacks never finish. `WallpaperThumbnailTests` checks distinct GIF/video frames after each temporary conversion buffer is disposed, and real process GDI object counts across repeated successful and failed saves. Conversion buffers are released per frame; the GIF encoder still retains its output frames until saving. These checks do not create WebView2 controls or verify native preview-window shutdown; closing during WebView/server initialization and time-perception cleanup still require real UI acceptance.

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
5. Edit without saving, cancel project Save As, then choose an empty destination folder. Verify the copy contains the unsaved edit and empty asset directory, the source bytes remain unchanged, and the editor switches to the copy.
6. Save another edit into the copy, export it, and reopen both projects independently. Check the copied manifest name and original file contents.

Context menus and keyboard input operate only on owned windows with foreground and obstruction checks. No DOM injection, test-only save endpoint, or substituted service is used. Preview quality, every editor command, OCR, and pixel-level rendering are outside this case. `WebProjectCopyTests` separately verifies complete copies, text encodings, source preservation, manifest edits, rejected destinations, cancellation before work and during unsaved-edit preparation, locked-source failure cleanup, and the contents of wallpaper/full-project ZIPs. View-model tests cover reopening evicted files and preserving the source file's dirty state during single-file Save As; they do not replace acceptance of the complete editor session switch. `FileUtilTests` also verifies the legacy copy overload's overwrite and recursion behavior.

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

The same case also cancels tray exit while inference is active, navigates away before checking window close, and checks temporary/exported/imported result badges. Cancelling exit or a save picker preserves the result and reminder preference; deleting a temporary result requires confirmation. A second real result verifies confirmed reminder suppression, UI reopening, and re-enabling the reminder in settings. Unfinished tasks always require confirmation, independently of this preference.

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

Core metadata tests verify relative basic/runtime JSON paths, relocation of a complete wallpaper directory, legacy absolute-path loading, and empty optional paths. Runtime consumers use resolved absolute paths; packaging must preserve the complete directory and its relative layout. The UI cases independently resolve paths read from raw JSON against that JSON file's directory before checking actual files.

Migration tests cover legacy rebuild routing, unchanged current-version files, invalid/future version rejection, runtime version validation before writes, cancellation, and retry after a partial migration. Failed writes must not advance the live metadata version.

`Passed` means the case's stated interaction and persistence assertions passed, not that the whole application is healthy. An environment skip is not a pass. `Evidence.RuntimeErrors` retains ERROR logs separately, and the runner warns about them even when the exit code is 0. Failed cases snapshot Core/UI logs and configuration before later process generations can rotate the logs.

Child-process-only environment variables carry a test session GUID and data root. Common reads them during initialization to isolate settings, logs, Core/UI singletons, pipes, and shared file locks. Process tests **do not enable `Constants.IsTestMode`**: shader paths, messages, and desktop mounting checks retain production behavior. Regular unit tests can still use `IsTestMode`; data isolation is independent of that flag.

Explicit sessions skip startup registration, startup notifications, and pending plugin updates, while settings still save through production code. Real locking and registry-modification test cases are **skipped** under the project test policy, are not registered, and do not request permission. Process cases keep screen saver locking disabled and skip wallpaper refresh with `SPIF_UPDATEINIFILE`, which would persist system settings. `SkippedSystemOperations` records these limits; they are not counted as passes. Without the environment variables, application behavior is unchanged. Tests do not read or write the user's wallpaper library or production configuration.

Component cases run serially on the UI thread. Cross-process UI Automation runs on a separate MTA thread; see [Microsoft's UI Automation threading guidance](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading). Component cases use recording implementations for effect services and apply callbacks. Application cases use the real backend. Library, screen-fit, and effect cases start a real player and verify desktop mounting within their stated limits.

The default suite does not validate WebView rendering quality, multi-monitor layouts, video playback policies, or AI inference. The seven regular test projects remain in their existing locations. `Core.Test/T_Workflows` continues to cover file, service, and gRPC integration rather than moving into the UI host.

## Installer smoke and independent UI acceptance

CI SDK setup, NuGet cache and artifact upload/download actions use a bundled Node.js 24 runtime and require Actions Runner `2.327.1` or later; this is independent of local/Web script Node.js settings. Transfer version and digest-check details are documented in the [workflow guide](../.github/workflows/README.md).

After build and parallel project tests, CI runs **Installer Smoke** and **Real UI Acceptance** in independent jobs. They do not share an installation directory or installer; UI runs the Release output. The local complete runner executes smoke including uninstall first, then standalone UI to avoid foreground contention.

`Run-InstallerTests.ps1` only runs required smoke against one installer in a unique temporary installation directory:

1. Install in English. The installed Core opens UI through its real first-run path; verify language through UI, RPC and saved settings. Set test theme/animation preferences, import an actual PNG, and exit UI/Core normally. Retain the isolated profile and wallpaper hashes.
2. Reinstall the same package in Chinese over this run's own installation. Check package identity, program hashes, version, registration, shortcuts and language marker.
3. Open the retained profile through Core's production ShowUI RPC; verify original English, preferences and wallpaper files/UID. Verify Chinese initialization with a separate empty profile. Both sessions must exit normally.
4. Always attempt the real uninstall, including after earlier failures; check process, file, shortcut and registration residue. Forced cleanup is recovery, never a passing normal exit.

Schema-3 `InstallerSmoke` reports contain Install, Open, Reinstall, VerifyPreserved, OpenFresh and Uninstall. **Every stage must pass without skips** in both workflows. Without a desktop, silent install/reinstall/uninstall diagnostics may be retained, but skipped startup/profile checks fail smoke. `-InstallerLanguage chinese` reverses the languages.

Installer smoke changes actual installed files, OS uninstall registration and shortcuts. Use a clean CI machine or explicitly prepared local administrator session. The existing-installation/AppId/shortcut guards remain strict; preserve user data. This is separate from UI cases that exclude persistent system-setting changes.

```powershell
# Required smoke on a clean Windows test machine:
./test/Run-InstallerTests.ps1 -InstallerPath <installer.exe> -ReportPath TestResults/Installer/installer-results.json
# Optional reverse order: Chinese first, then English reinstall:
./test/Run-InstallerTests.ps1 -InstallerPath <installer.exe> -InstallerLanguage chinese -ReportPath TestResults/InstallerChinese/installer-results.json
# Independent UI against the complete prebuilt Release output (no installation):
./test/Run-UIOperationTests.ps1 -Configuration Release -NoBuild -Suite Extended
# Contracts only: no installation, UI startup or registry writes:
./.github/scripts/validate-test-workflow.ps1
# Interactive fixture only, when a desktop is available:
./test/Test-InstallerWizard.ps1
```

Only the CI `test-ui-operation` job uses `continue-on-error`. It records `steps.ui-tests.outcome` and uploads raw reports even on failure. The summary takes `-AllowUIFailure -UIJobResult <outcome>` and retains UI failures, timeout, missing/unreadable reports and explicit desktop skips without blocking PR success or release version/draft work. Smoke/project failures and missing smoke reports always block. Pre-release also rejects skipped selected project tests. Local UI is strict by default; `-RequireFullAcceptance` without the UI opt-in rejects UI skips too.

The workflow contracts include isolated Git tests for a single version commit on `main`, merging that history into `dev`, preserving newer development, conflict cleanup, retries, the next release, and rejected atomic pushes. These tests use local fixture remotes and never push to GitHub.

## Adding coverage

Built-in wallpaper folders are maintained under [`resources/BuiltInWallpapers`](../resources/BuiltInWallpapers/README.md). The build includes them in Core output; setup reads `BuiltInWallpapersDir`, falling back to a sibling of `WallpaperDir` for older profiles, and adds missing folders while preserving existing folders and edits. Installer acceptance supplies its isolated data root to setup, so this deployment never writes into the daily library. Core and UI logic tests cover legacy defaults, persisted independent paths through RPC/restart, combined scanning/deletion, preview settings, moving both libraries through one directory picker, index refresh, and preserving source data on failure. The authored metadata inventory is embedded into artifact tests, so a missing build folder or stale item fails. Core workflow tests relocate and decode the actual collection (including animated frames and prepared depth maps), scan it alongside a user item, and check that neither source assets nor user data changed. Real UI verifies multi-file import and prepared-item playback as described above. Installer smoke verifies complete initial deployment, then moves the isolated built-in library, edits an existing title, adds a user file, deletes an existing notice and removes another whole item. The real reinstall must honor the saved directory, preserve the edited folder exactly (including its deleted file), and deploy the missing item. Uninstall must preserve this library. These checks run inside required smoke stages; `BuiltInWallpapers` in the report records their evidence. `Test-BuiltInWallpaperTestSupport.ps1` checks positive and negative validator contracts without installing; it does not simulate or replace the actual ISS acceptance.

Implement new cases under `Cases` and register them in `App.xaml.cs`; the runner and CI do not maintain hardcoded case names or counts.

- Commit shared test infrastructure and the smallest real scenario separately.
- Add one module per subsequent commit, including any necessary shared helpers.
- Verify real interaction, backend persistence, reopened state, and process cleanup before moving to the next module.
- Document uncovered behavior and known errors in reports. Keep cross-module fixes in separate commits.
- Do not commit reports, runtime logs, or unrelated local changes with a feature.
