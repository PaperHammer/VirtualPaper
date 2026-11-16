# Asset tools

[English](README.md) | [简体中文](README.zh-CN.md)

Run these tools manually from the repository root. Application startup and normal builds do not invoke them.

| Tool | Entry point | Purpose |
| --- | --- | --- |
| Bundled wallpapers | [BuiltInWallpapers](BuiltInWallpapers/README.md) | Prepare source media and generate or verify wallpaper metadata and previews. |
| Store materials | [Store materials guide](../store/README.md) | `Start-StorePreview.ps1` opens an isolated preview; `Capture-StoreScreenshot.ps1` exports a pointer-free native PNG; `Export-StoreMaterials.ps1` validates/exports localized copy and images. These local tools do not upload or publish. |
| Store Hero and overview | [Generate-StoreArtwork.py](Generate-StoreArtwork.py) | Compose the soft-focus 4K Hero and render the existing README hero as the first screenshot, using a private headless Chrome/Edge. Requires Python, Pillow and websocket-client. |
| Application branding | [Generate-BrandAssets.ps1](Icons/Generate-BrandAssets.ps1) | Render the shared [vector icon](../icons/virtualpaper.svg) and [logo sources](../logos/README.md) into application/installer ICOs, MSIX images, download badges, splash screens, logos and the README brand image. Requires Windows and an STA PowerShell. |
| Store icons | [Generate-StoreIcons.ps1](Icons/Generate-StoreIcons.ps1) | Export transparent and blue/violet tile presentations at 300/512/1024 px from the same vector icon. Included in full branding generation. |
| PNG-to-ICO utility | [gen_appicon_ico.ps1](Icons/gen_appicon_ico.ps1) | Interactive conversion of other PNG assets. Application branding uses the vector generator above. |
| Project file icons | [Generate-ProjectFileIcons.ps1](Icons/Generate-ProjectFileIcons.ps1) | Render self-contained `.vpd`/`.vpw` SVG sources to PNG and seven-size ICO. Lettering is outlined and requires no fonts at render time. Run `pwsh -NoProfile -STA -File resources/tools/Icons/Generate-ProjectFileIcons.ps1` on Windows. |

```powershell
pwsh -NoProfile -STA -File resources/tools/Icons/Generate-BrandAssets.ps1
```

Edit the SVG source, then regenerate the assets. Paths and pixel dimensions are preserved; no font installation or Python image libraries are needed.

Test and acceptance entry points remain under [`test`](../../test/README.md); workflow contracts use [`.github/scripts/validate-test-workflow.ps1`](../../.github/scripts/validate-test-workflow.ps1). The ignored root `scripts/` directory is reserved for local temporary tools.
