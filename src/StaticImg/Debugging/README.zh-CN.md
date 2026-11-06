# StaticImg 本地调试开关

[English](README.md) | [简体中文](README.zh-CN.md)

调试开关定义在 `StaticImgDebugSwitches.cs` 中，仅在 Debug 构建中可用，Release 构建会强制关闭。

## 笔画缓存叠加层

叠加层用于显示稳定笔画缓存、当前提交区域、活动笔画边界和画布脏区。无需修改源码即可临时开启：

1. 启动调试，等待 StaticImg 界面加载完成。
2. 在 Visual Studio 中选择 **调试 > 全部中断（Debug > Break All）**。
3. 在即时窗口中执行：

```csharp
Workloads.Creation.StaticImg.Debugging.StaticImgDebugSwitches.ShowStrokeCacheOverlay = true
```

4. 按 F5 继续运行，然后开始绘制或移动笔画，以刷新叠加层。

进程运行时，即时窗口可能提示无法计算表达式，请先暂停执行。

若希望本地每次启动都开启，可将 `ShowStrokeCacheOverlay` 属性的初始值改为 `true`。这会在每次 Debug 启动时显示叠加层。进行性能测量或提交修改前，请恢复为 `false`。

在调试器中关闭：

```csharp
Workloads.Creation.StaticImg.Debugging.StaticImgDebugSwitches.ShowStrokeCacheOverlay = false
```
