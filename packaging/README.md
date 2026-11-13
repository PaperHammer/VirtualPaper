# Packaging

[English](README.md) | [简体中文](README.zh-CN.md)

Run commands from the repository root. Build the complete Release solution first using the [development guide](../doc/development-guide.md). Shared build, version and release automation stays in [`.github/scripts`](../.github/scripts).

| Channel | Definition | Local build entry point | Default generated output |
| --- | --- | --- | --- |
| EXE | [inno/setup.iss](inno/setup.iss) | `ISCC.exe packaging/inno/setup.iss` | `packaging/inno/Output/` |
| MSIX | [msix/VirtualPaper.Package.wapproj](msix/VirtualPaper.Package.wapproj) | `pwsh -NoProfile -File .github/scripts/Build-Msix.ps1 -SkipSolutionBuild` | `packaging/msix/AppPackages/<run>/` |

EXE compilation requires Inno Setup 6.7.1; the example assumes `ISCC.exe` is on `PATH`. MSIX requires Visual Studio MSBuild and MSIX Packaging Tools; see the [MSIX guide](msix/README.md) for preview signing and Store identity parameters. Compiling either package does not install or launch the application.

Both channels use the shared sources under [`localization`](../localization/README.md). Direct ISS compilation runs resource generation automatically; the MSIX project generates its manifest before packaging. Source-relative paths in these definitions resolve from their respective `inno/` and `msix/` directories.

Generated `Output/`, `AppPackages/`, `bin/` and `obj/` contents are not committed. The CI wrappers `.github/scripts/New-CIExePackage.ps1` and `.github/scripts/New-CIMsixPackage.ps1` keep their existing entry points and produce channel artifacts under `dist/` by default.

Build verification is separate from installed acceptance. See the [testing guide](../test/README.md) for `test/Run-Tests.ps1` and `test/Run-MsixTests.ps1`, and the [workflow guide](../.github/workflows/README.md) for required CI gates. Installer and real UI runs require their documented clean-install and desktop prerequisites.
