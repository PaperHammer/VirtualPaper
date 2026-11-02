# Business workflow tests

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
