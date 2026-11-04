# StaticImg local debugging switches

[English](README.md) | [简体中文](README.zh-CN.md)

Debugging switches are defined in `StaticImgDebugSwitches.cs`. They are available in Debug builds and forced off in Release builds.

## Stroke cache overlay

The overlay shows the stable stroke cache, current commit bounds, active stroke bounds, and dirty canvas regions. To enable it temporarily without editing source:

1. Start debugging and wait for the StaticImg UI to load.
2. Select **Debug > Break All** in Visual Studio.
3. Run this in the Immediate Window:

```csharp
Workloads.Creation.StaticImg.Debugging.StaticImgDebugSwitches.ShowStrokeCacheOverlay = true
```

4. Press F5 to resume, then start or move a stroke to refresh the overlay.

The Immediate Window may report that it cannot evaluate the expression while the process is running. Pause execution first.

For a persistent local setting, change the `ShowStrokeCacheOverlay` property initializer to `true`. This enables the overlay at each Debug startup. Restore it to `false` before performance measurements or committing changes.

To disable it in the debugger:

```csharp
Workloads.Creation.StaticImg.Debugging.StaticImgDebugSwitches.ShowStrokeCacheOverlay = false
```
