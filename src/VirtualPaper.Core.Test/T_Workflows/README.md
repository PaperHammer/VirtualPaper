# Business workflow tests

[English](README.md) | [简体中文](README.zh-CN.md)

Run with:

```powershell
dotnet test src/VirtualPaper.Core.Test/VirtualPaper.Core.Test.csproj -p:Platform=x64 --filter TestCategory=Workflow
```

`SettingsWorkflowTests` runs the production settings client over a unique named pipe,
the production gRPC server and settings service, and real JSON storage in a temporary
directory. It checks save/reload/service recreation, legacy optional-field behavior,
and recovery from corrupt JSON. Only display discovery, UI restart and OS auto-start
configuration are substituted; no production pipe or user data directory is used.

Workflow classes are not parallelized because the existing CommonPaths test root is
process-wide. Each environment restores the previous root and test-mode flag.

`WallpaperWorkflowTests` imports a real PNG through the production wallpaper client
and gRPC server, creates real metadata/thumbnail/index files, applies/closes it via
the real WallpaperControl, and checks persisted layouts. It exercises the library
deletion path, cancellation, in-use protection, reindexing after deletion, invalid
imports and failed player startup in all three layouts. The bound collection is
seeded from the real index without creating a XAML loading overlay. Only dialogs,
display discovery, native desktop calls and the external player process are
substituted. It does not assert pixels rendered on the desktop.

`RecentFeaturesWorkflowTests` runs ScreenFitViewModel with the real clients and
backend, real input/output PNGs, metadata, runtime configs and library index. It
covers contain/cover, optional enhancement, mid-operation cancellation, model
failure and display removal. The deterministic upscaler substitutes only model
inference; the existing ML Integration suite remains responsible for real models.
Image and depth-image effect configs are refreshed repeatedly, applied, stopped,
and reopened after deleting preview caches. Missing/locked configs must fail.

`BuiltInWallpaperWorkflowTests` relocates the actual collection into an isolated
library alongside a user image. It decodes images, GIF/video frames and depth maps,
validates Web entry metadata, checks the production index, and verifies that source
assets and the user item are unchanged. Actual ISS deployment and desktop playback
are covered separately by installer smoke and WallpaperLibrary UI cases.

These workflows do not launch real XAML pages or verify rendered pixels. Desktop
UI interaction is covered separately by [UIOperationTest](../../../test/README.md).
Rendering/performance acceptance still requires separate validation.
