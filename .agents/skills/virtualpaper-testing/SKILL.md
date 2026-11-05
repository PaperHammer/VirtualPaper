---
name: virtualpaper-testing
description: Run, select, extend or diagnose VirtualPaper local tests and installed-app acceptance; trace their relationship to Pre-merge Check and Pre-release workflows. Use for changes to test runners, UI acceptance, packaging verification or workflow test gates.
---

# VirtualPaper testing

Run commands from the repository root in PowerShell 7.2+. Read [AGENTS.md](../../../AGENTS.md) for the process/module map and execution boundaries. Use the existing runners; do not recreate their installer handling, process isolation or report validation in a temporary script.

## Select the test layer

| Request or changed area | Entry point |
| --- | --- |
| Complete local regression, before-merge installed acceptance | `test/Run-Tests.ps1` |
| Core, UI logic, image/Web editor logic, reusable components, ML, shaders or packaged files | `test/Run-ProjectTests.ps1` after the complete Release build |
| Actual controls, navigation, language/theme, tray, editors, UI process lifecycle | `test/Run-UIOperationTests.ps1` |
| A specific real installer, first-run language or install/uninstall behavior | `test/Run-InstallerTests.ps1` |
| Workflow DAG, report gates, desktop detection, version/release scripts | `.github/scripts/validate-test-workflow.ps1` |

Read the [testing guide](../../../test/README.md) ([中文](../../../test/README.zh-CN.md)) for module coverage and prerequisites. Read the [workflow guide](../../../.github/workflows/README.md) ([中文](../../../.github/workflows/README.zh-CN.md)) when working on CI, PR inputs, versioning or retries. Scripts and workflow YAML take precedence over stale examples or previous-run counts.

## Complete local acceptance

Requires Windows, elevated PowerShell, Visual Studio with the solution's Windows/WinUI tools, .NET 8 SDK, Node.js, Inno Setup 6.7.1 and the application runtime prerequisites. Real extended UI cases also require the bundled Real-ESRGAN and Depth Anything models; consult the testing guide for paths. A usable desktop is needed for real UI acceptance.

```powershell
pwsh -NoProfile -File test/Run-Tests.ps1
# Also select the regular ML project's optional integration category:
pwsh -NoProfile -File test/Run-Tests.ps1 -IncludeMLIntegration
# For a nonstandard Inno Setup installation:
pwsh -NoProfile -File test/Run-Tests.ps1 -InnoSetupCompiler 'C:\tools\InnoSetup\ISCC.exe'
```

The runner validates workflow contracts, rebuilds the full Release solution, runs eight project jobs in parallel, compiles the production ISS, then installs → verifies first launch and normal exit → runs extended UI against the installed Core/UI → uninstalls → validates the combined reports. A failure blocks later phases, while installation cleanup still executes. It creates a fresh `TestResults/local-acceptance-*` directory; report the exact directory used.

Installer runs change actual installed files, shortcuts and uninstall registration. Check the user's authorization and any current constraints before executing them. The clean-install guard rejects an existing daily installation; do not bypass it or automatically uninstall that application. Preserve user data. Test runs may occupy the foreground; honor any request to defer real UI interaction.

`-IncludeMLIntegration` controls the regular ML project only. The complete runner always requests the extended UI suite, which already includes real inference and depth rendering. Missing UI models fail that suite. Do not silently substitute the standard suite.

To inspect the result with release-strength no-skip validation, use the same completed result directory:

```powershell
pwsh -NoProfile -File test/Summarize-TestResults.ps1 -Root '<result-directory>' -RequireFullAcceptance
```

This validates existing evidence; it does not run missing tests, package a new release or synchronize versions. If selected ML integration cases were skipped for missing model files, strict validation fails.

## Focused development

Build application/UI/test dependencies together through the solution, using Visual Studio MSBuild:

```powershell
msbuild src/VirtualPaper.sln /t:Build /p:Configuration=Release '/p:Platform=Any CPU' /restore /m
pwsh -NoProfile -File test/Run-ProjectTests.ps1 -JobId test-core -ResultsDirectory "TestResults/core-review-$([guid]::NewGuid().ToString('N'))"
pwsh -NoProfile -File test/Run-ProjectTests.ps1 -JobId test-ui-logic -ResultsDirectory "TestResults/ui-review-$([guid]::NewGuid().ToString('N'))"
```

Use Developer PowerShell or locate MSBuild via `vswhere.exe`; do not hard-code another machine's Visual Studio path. Read `Get-ProjectTestSpecs` in `test/TestReportSupport.ps1` for all eight valid job IDs and their report paths. The focused project runner uses `--no-build`; UI project script checks also run separately in CI:

```powershell
node .github/scripts/test-atmosphere.cjs
node .github/scripts/test-depth-player.cjs
```

For actual UI operations:

```powershell
pwsh -NoProfile -File test/Run-UIOperationTests.ps1 -Configuration Release -NoBuild -Module GeneralSettings
pwsh -NoProfile -File test/Run-UIOperationTests.ps1 -Configuration Release -NoBuild -Suite Extended
```

Omit `-NoBuild` to have the UI runner build the complete solution first. `-Module` selects application cases by name; consult `test/VirtualPaper.UIOperationTest/App.xaml.cs` for valid names. It cannot be combined with `-Suite Extended`. Component cases run with the standard/extended suites. `VirtualPaper.UIOperationTest` is a WinUI executable, not a `dotnet test` project. Do not use Sandbox, mock away the backend or directly launch UI in place of the Core production path. Tests must retain session isolation, real persistence checks and normal-exit assertions.

For an existing installer plus matching prebuilt hosts:

```powershell
pwsh -NoProfile -File test/Run-InstallerTests.ps1 -InstallerPath '<setup.exe>' -NoBuild -RunUIOperationTests `
  -UIHostExe test/VirtualPaper.UIOperationTest/bin/x64/Release/net8.0-windows10.0.19041.0/VirtualPaper.UIOperationTest.exe
```

Use `-InstallerLanguage chinese` to exercise Chinese install/first-run behavior; English is the default. A valid existing settings file preserves its language on upgrade. Application and host dependencies must match; if hash checks fail, rebuild the complete solution and regenerate the installer. Rerunning an old installer does not test a new source fix.

## Results, failures and skips

- Read exit status **and** fresh raw reports. A process exit code of zero alone does not establish that UI ran.
- Project jobs produce TRX and, for the seven regular projects, Cobertura. Coverage measures executed source lines, not UI completeness. `TestReportSupport.ps1` validates counts, outcomes and unhandled exceptions.
- `test-results-installer/installer-results.json` records install/open/UI/uninstall; `test-results-ui-operation/ui-operation-results.json` records actual cases. `.artifacts` folders retain logs, isolated settings and environment evidence. Use `Summarize-TestResults.ps1` for combined validation.
- Headless runs install/uninstall silently and explicitly skip startup/real UI. A valid UI skip has `Status=Skipped`, `Passed=false`, `ExpectedCount=0` and desktop evidence. Never report it as UI passed. An activation failure or timeout after startup remains a failure.
- A Codex sandbox desktop is not the interactive Default desktop. If execution is authorized, use the environment's supported elevated/unsandboxed execution path for real desktop tests; do not change desktop eligibility checks.
- Session locking, startup registration and persistent system-setting UI cases remain excluded. Real idle tests are opt-in and require no input; do not include them in routine acceptance without the user's arrangement.
- Preserve failed reports. Fix the demonstrated failure and rerun with a new report path. Do not mask problems by deleting checks, increasing timeouts without evidence, or treating forced cleanup as normal exit.

## Workflow changes

Both workflows are manual `workflow_dispatch` only. Pre-merge Check validates an open PR and its fixed merge snapshot, builds once, shares that output across project tests and installed acceptance, then reports status on the PR head. Pre-release verifies the checked merged `dev → main` PR, sets the target version before build/package, repeats tests and installed acceptance, rejects selected-test skips, then synchronizes product versions for a regular release and creates a draft. Plugin-only releases retain the product version and skip product-version synchronization. Local acceptance performs no remote release/status actions.

After changes to scripts, reports or workflow dependencies, run:

```powershell
pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1
```

This exercises isolated/mock contracts without installing, locking, writing system registration or pushing. It does not replace actual build, install or UI acceptance. Keep the DAG, shared build/runner references and strict release gates intact. Update the English/Chinese test and workflow guides alongside changed behavior; update `AGENTS.md` only when its stable entry points or architecture change.
