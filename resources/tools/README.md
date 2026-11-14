# Asset tools

[English](README.md) | [简体中文](README.zh-CN.md)

Run these tools manually from the repository root. Application startup and normal builds do not invoke them.

| Tool | Entry point | Purpose |
| --- | --- | --- |
| Bundled wallpapers | [BuiltInWallpapers](BuiltInWallpapers/README.md) | Prepare source media and generate or verify wallpaper metadata and previews. |
| Application icon | [gen_appicon_ico.ps1](Icons/gen_appicon_ico.ps1) | Convert a source PNG into a multi-size ICO. Requires Windows and System.Drawing. |
| Project file icons | [Generate-ProjectFileIcons.ps1](Icons/Generate-ProjectFileIcons.ps1) | Render self-contained `.vpd`/`.vpw` SVG sources to PNG and seven-size ICO. Lettering is outlined and requires no fonts at render time. Run `pwsh -NoProfile -STA -File resources/tools/Icons/Generate-ProjectFileIcons.ps1` on Windows. |

```powershell
pwsh -NoProfile -File resources/tools/Icons/gen_appicon_ico.ps1
```

Enter the source PNG path, then the destination ICO path. Leave the destination blank to write an ICO beside the source with the same base name.

Test and acceptance entry points remain under [`test`](../../test/README.md); workflow contracts use [`.github/scripts/validate-test-workflow.ps1`](../../.github/scripts/validate-test-workflow.ps1). The ignored root `scripts/` directory is reserved for local temporary tools.
