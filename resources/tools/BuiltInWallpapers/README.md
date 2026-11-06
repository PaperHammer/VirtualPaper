# Authoring built-in wallpapers

[中文](README.zh-CN.md)

Open [`preview.html`](preview.html) to inspect the actual video, GIF and interactive Web item together.

## Media and permissions

| Item | Creator | Source and terms |
| --- | --- | --- |
| Alpine Stillness | VirtualPaper | Generated mountain lake, original 1672×941 PNG and depth cache |
| Emerald Morning | VirtualPaper | Generated hills, original 1672×941 PNG and depth cache |
| Bigora Cascade | Termininja | [Bigora waterfall](https://commons.wikimedia.org/wiki/File:Bigora_waterfall.webm), own work, CC0 1.0 |
| Santa Rosa Waves | Blervis | [Santa Rosa Island Waves](https://commons.wikimedia.org/wiki/File:Santa_Rosa_Island_Waves.webm), own work, CC0 1.0 |
| Luminous Flow | VirtualPaper | Original Canvas animation, GPL-3.0, inspired by `src/WebBackdrop/Assets/templates/v1` |

The two still images were created with the built-in image generation tool. They are synthetic scenes, not photographs of real locations; no Unsplash screenshot artwork is included. [`artwork.json`](artwork.json) retains their full prompts. Original PNGs live inside the wallpaper folders, without upscaling. The Web wallpaper contains no photographs, HUD or clock.

The GIF and video use **different real camera recordings**, checked on 2026-11-06. Each creator explicitly applies CC0 to their own footage on its file description page. [`media.json`](media.json) records authors, permanent page revisions, download URLs, original SHA-256 hashes and exact processing parameters. Large original downloads are cached only under ignored `TestResults/built-in-wallpapers/external`.

[CC0](https://creativecommons.org/publicdomain/zero/1.0/) permits copying, modification, commercial use and redistribution, including standalone media, without attribution or share-alike conditions. VirtualPaper applies CC0 to any copyright it holds in these media adaptations and thumbnails too. Their media rights are independent of the application code's GPL license. Creator/source notices are for provenance, not an extra condition. CC0 does not clear unrelated third-party rights or provide warranties. Each external media folder ships its notice and the [full official CC0 text](CC0-1.0.txt).

## Prepare and package

After a complete Release solution build, use Python 3.11+ with Pillow and websocket-client, Chrome/Edge, an H.264-capable ffmpeg executable, and the project's existing Depth Anything V2 model:

```powershell
# Downloads the two pinned CC0 originals if absent, checking SHA-256 before use.
python resources/tools/BuiltInWallpapers/prepare.py --ffmpeg 'C:/tools/ffmpeg.exe'
dotnet run --project resources/tools/BuiltInWallpapers/Generate.csproj -c Release -- 'E:/repos/codes/VirtualPaper'
# Validate existing items without regeneration or downloads:
dotnet run --project resources/tools/BuiltInWallpapers/Generate.csproj -c Release --no-build -- 'E:/repos/codes/VirtualPaper' --verify
# Re-encode only the camera footage and refresh thumbnails:
python resources/tools/BuiltInWallpapers/prepare.py --motion-only --ffmpeg 'C:/tools/ffmpeg.exe'
```

Bigora Cascade uses seconds 3–9 of the original, a fixed 1280×720 crop below the flag, a downscale to 768×432, and a 0.4-second loop crossfade. Its GIF has a shared palette and 20 fps cadence. Identical first/last frames share a 50 ms interval (20 + 30 ms), avoiding an extra pause. Santa Rosa Waves uses seconds 20–40 of the original 1080p recording and a 0.6-second loop crossfade, encoded as silent H.264 at 30 fps. Both stay at real-time speed. No optical-flow interpolation, generated motion, reversed water flow or upscaling is used.

`prepare.py` uses a private invisible browser and a loopback-only server to check actual pointer trails, click bursts, pause/resume and reduced-motion behavior in Luminous Flow. Its preview comes from that rendering. Temporary inputs, browser profiles and logs stay under ignored `TestResults/built-in-wallpapers`.

`Generate.csproj` consumes existing Release assemblies without starting the application. It calls the same image/video/Web builders as `WallpaperControl.CreateBasicData`, replaces large thumbnails with compact library previews, saves through `WpBasicData.SaveAsync`, and prepares both image depth maps through `DepthMapCache.GetOrCreateAsync`. It checks that the production scanner finds all five items, metadata paths are relative and internal, author/tags/licenses are present, and depth caches are reused without inference. Reflection is limited to Core's internal builders; the production API is unchanged.

All items carry `Built-in;内置` tags. Only generated still images carry `AI-generated`; the real recordings carry `CC0` and their original authors. Folder names match displayed titles. Keep folder names and UIDs stable after publishing: the installer deliberately preserves existing folders, so a replacement intended as a new wallpaper needs a new folder name. The authoring commands overwrite known repository outputs, not installed user libraries.
