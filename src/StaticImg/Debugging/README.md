# StaticImg 本地调试开关

调试开关集中定义在 `StaticImgDebugSwitches.cs`，仅在 Debug 构建中生效，
Release 构建会强制关闭。

## 稳定笔画动态缓存覆盖层

临时开启且不修改源码时：

1. 启动调试并等待 StaticImg 界面加载。
2. 使用“调试 → 全部中断”，让进程进入中断模式。
3. 在 Visual Studio 即时窗口执行：

```csharp
Workloads.Creation.StaticImg.Debugging.StaticImgDebugSwitches.ShowStrokeCacheOverlay = true
```

4. 按 F5 继续运行，并重新开始或移动一次笔画以刷新覆盖层。

即时窗口在进程仍处于运行模式时可能提示“无法计算表达式”，这是调试器限制。

最稳定的配置方式是将 `ShowStrokeCacheOverlay` 的属性初始值改为 `true`，让它在
每次 Debug 启动后默认开启。性能测试和提交代码前建议恢复为 `false`。

关闭时执行：

```csharp
Workloads.Creation.StaticImg.Debugging.StaticImgDebugSwitches.ShowStrokeCacheOverlay = false
```
