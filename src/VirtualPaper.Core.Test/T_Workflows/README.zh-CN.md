# 业务流程测试

[English](README.md) | [简体中文](README.zh-CN.md)

运行命令：

```powershell
dotnet test src/VirtualPaper.Core.Test/VirtualPaper.Core.Test.csproj -p:Platform=x64 --filter TestCategory=Workflow
```

`SettingsWorkflowTests` 使用正式的设置客户端、独立命名管道、正式的 gRPC 服务端与设置服务，以及临时目录中的真实 JSON 存储。覆盖保存、重新加载、服务重建、旧版本可选字段行为，以及损坏 JSON 的恢复。仅替换显示器发现、UI 重启和系统自启动配置，不使用正式管道或用户数据目录。

由于现有 CommonPaths 测试根目录属于进程级状态，业务流程测试类不并行运行。每个测试环境都会恢复之前的根目录和测试模式标记。

`WallpaperWorkflowTests` 通过正式的壁纸客户端和 gRPC 服务端导入真实 PNG，创建真实的元数据、缩略图和索引文件，通过真实的 WallpaperControl 应用、关闭壁纸，并检查持久化的布局。覆盖三种布局下的壁纸库删除、取消、使用中保护、删除后重建索引、无效导入，以及播放器启动失败。绑定集合的数据来自真实索引，不创建 XAML 加载遮罩。仅替换对话框、显示器发现、原生桌面调用和外部播放器进程，不断言桌面实际渲染的像素。

`RecentFeaturesWorkflowTests` 使用真实客户端和后台运行 ScreenFitViewModel，使用真实的输入、输出 PNG、元数据、运行时配置和壁纸库索引。覆盖 contain/cover、可选增强、操作中途取消、模型失败和显示器移除。确定性超分替身仅替代模型推理；真实模型仍由现有 ML Integration 测试负责。图片和深度图的效果配置会经过反复刷新、应用、停止，并在删除预览缓存后重新打开。配置缺失或被锁定时必须失败。

这些业务流程测试不会启动真实 XAML 页面，也不验证渲染像素。桌面 UI 交互由独立的 [UIOperationTest](../../../test/README.zh-CN.md) 覆盖。渲染效果与性能验收仍需单独验证。
