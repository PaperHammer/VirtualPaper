# Built-in wallpapers

[中文](README.zh-CN.md)

Place complete, already imported VirtualPaper wallpaper folders here, one stable folder name per wallpaper:

```text
BuiltInWallpapers/
  <wallpaper-id>/
    wp_metadata_basic.json
    ...media, thumbnail, depth cache and effect files...
```

Use format 19 with relative paths. Keep the original folder name and wallpaper UID stable. Include every referenced asset inside its wallpaper folder. For 3D images, keep the generated `depth-<hash>.png` filename; a changed source image or model can require regeneration. Runtime configuration for another machine's monitor does not replace configuration for the user's monitors.

The application build copies these folders to `BuiltInWallpapers/` in the Core output. These two README files are excluded. An empty resource directory is also supported.

## Included collection

| Title | Type | Contents |
| --- | --- | --- |
| Alpine Stillness | Image / 3D image | Generated mountain lake, with a prepared depth cache |
| Emerald Morning | Image / 3D image | Generated green hills, with a prepared depth cache |
| Bigora Cascade | GIF | Real waterfall footage by Termininja, CC0 |
| Santa Rosa Waves | Video | Real 1080p coastal footage by Blervis, CC0 |
| Luminous Flow | Web | Pointer light trails and click bursts inspired by the default Web template |

All items have `Built-in;内置` tags and complete titles, descriptions and dates. The two still images are AI-generated landscapes attributed to VirtualPaper, with original 1672×941 PNGs and prepared depth caches; no Unsplash screenshot artwork is included. Luminous Flow is original Canvas code without photo assets.

The GIF and video use two distinct, real camera recordings. Bigora Cascade is a cropped 768×432, 20 fps GIF; Santa Rosa Waves is silent 1920×1080, 30 fps H.264. Playback stays at real-time speed, with only short loop crossfades. They retain their original authors and `CC0` tags, without `AI-generated` tags. Both creators explicitly dedicated their own footage under CC0: [Bigora waterfall](https://commons.wikimedia.org/wiki/File:Bigora_waterfall.webm) and [Santa Rosa Island Waves](https://commons.wikimedia.org/wiki/File:Santa_Rosa_Island_Waves.webm). CC0 permits commercial use, modification and redistribution, including standalone media, without attribution or share-alike conditions. VirtualPaper adds no restrictions to these media adaptations. CC0 does not clear unrelated third-party rights or provide warranties.

Each folder includes `NOTICE.txt`; the external recordings also include the full CC0 legal text. [Authoring instructions](../tools/BuiltInWallpapers/README.md), source URLs, pinned source hashes and processing parameters are retained separately from the installer payload. Original downloads remain in ignored `TestResults`, rather than duplicating large footage in Git.

The full installer deploys new folders to `BuiltInWallpapersDir` saved in `user_settings.json`. Its default is `<library root>/BuiltInWallpapers`, next to `<library root>/wallpapers`; the default root is `%LOCALAPPDATA%\VirtualPaper\Library`. For older profiles without this field, both the application and setup derive the default from `WallpaperDir`. It skips entire folders that already exist, preserving user changes and deleted files inside them. Deleting an entire wallpaper folder allows the next installation to restore it. Uninstall preserves these files unless the user chooses to remove all application data; a library moved outside the application data directory is left in place.

Settings displays one directory picker. Changing it moves both libraries to the selected root as `wallpapers` and `BuiltInWallpapers`, including playback layouts and runtime metadata, then saves both paths. An explicitly configured built-in source directory is also moved. The selected root may contain unrelated files, but must not be inside a source library. Copying refuses occupied library subdirectories and never overwrites existing files. Reselecting the current root does nothing when all library paths are unchanged. Originals are retained if preparation or restarting playback fails.

Before any file copying, the source scan also determines the longest relative path in each library. Its resulting full target path must fit the application's 259-character migration compatibility bound, including library folder names. This conservative policy covers shell/native consumers whose long-path behavior has not been validated; it is not the .NET path limit. See Microsoft's [Win32 path documentation](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation).

Copy checks use directory entries and file sizes without rereading file contents. A source snapshot records entry names, file sizes and modification times before copying; source changes detected after copying stop preparation. All libraries must finish preparation before paths are switched. After settings are saved and playback restoration reports success, cleanup only considers sources actually copied in this run and checks their snapshots again. Changed sources are retained; deletion failures are logged and may leave remnants at the old location. There is no fixed cleanup delay or blind deletion retry. This is a metadata-based change check, not a content-integrity guarantee or a transaction against crashes and concurrent writes after the final check.

The library scans this directory alongside the configured user wallpaper directory. Built-in wallpapers use the same preview, playback, edit and delete operations. No startup import or download is required. Plugin-only updates do not deploy this directory; new built-in wallpapers require a full installer.

Application library roots are registered in [`WallpaperLibraryDirectories`](../../src/VirtualPaper.Models/Cores/WallpaperLibraryDirectories.cs), which supplies directory names and settings accessors for scanning, migration and rollback. Additional roots also need their settings/RPC fields, default values and any installer payload handling.
