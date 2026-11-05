# 架构说明

## 运行时视图

```text
VirtualPaper.UI (WinUI 管理与创作界面)
        │ gRPC / 命名管道客户端
        ▼
VirtualPaper (WPF 后台核心进程)
        │ 创建、控制、暂停或销毁
        ├────────► VirtualPaper.PlayerWeb (Web/视频播放)
        ├────────► StaticImg / Shader 相关运行时
        └────────► VirtualPaper.ScreenSaver
```

`VirtualPaper/App.xaml.cs` 是后台核心的组合根，注册壁纸控制、显示器、播放策略、更新、原生系统服务和各 gRPC Server。`VirtualPaper.UI/App.xaml.cs` 是前端组合根，注册各 Panel、ViewModel、gRPC Client、文件加载器和创作运行时。

## 依赖方向

整体依赖可概括为：

```text
UI / Panels / Workloads
        │
        ├──► UIComponent ──► Models ──► Common
        ├──► Grpc.Client ──► DataAssistor / Grpc.Service
        └──► PlayerWeb.Core / ML / Shader
```

维护时应尽量保持底层共享项目不反向依赖具体页面。跨进程能力通过客户端接口暴露，页面和 ViewModel 不直接操作后台核心实例。

## 壁纸运行时选择

创作工作区通过 `IRuntimeFactory` 请求适合文件类型的运行时。当前 `Workloads.Entry/RuntimeFactory.cs` 的映射为：

| 文件类型 | 运行时 |
| --- | --- |
| `FDesign`, `FImage` | `StaticImg.MainPage` |
| `FWebDesign` | `WebBackdrop.MainPage` |

运行时创建后调用 `Initialize(file, type)`。新增创作文件类型时，需要同时检查文件类型识别、项目文件加载器、DI 注册和运行时工厂映射。

## 依赖注入与服务定位

两个主要进程分别构建自己的 `ServiceProvider`。部分 WinUI 控件通过 `AppServiceLocator.Services` 获取 ViewModel 或窗口服务，例如 `WebFileTreeControl` 获取瞬态 `WebFileTreeViewModel`。这意味着：

- 控件只能在服务定位器完成初始化后创建；
- 新增 ViewModel 时必须在对应进程的 `App.ConfigureServices` 中注册；
- 瞬态 ViewModel 的状态归控件/页面实例所有，单例 ViewModel 则需注意跨页面残留状态；
- 单元测试应优先构造 ViewModel 及其依赖，减少依赖全局服务定位器。

## 项目文件加载

管理 UI 注册多个 `IProjectFileLoader`：图片、设计文件和 Web 项目加载器，再由 `ProjectFileLoaderRegistry` 选择。文件加载器负责把磁盘文件转换为工作区可消费的项目；运行时工厂负责创建实际编辑界面，两者职责不同。

## 文件变化与保存

共享的 `VirtualPaper.Common.Utils.ProjectSystem` 提供文件系统监听和文档状态跟踪。WebBackdrop 在其上增加 `ProjectFileManager`：

1. 监听项目目录中的创建、修改、删除和重命名；
2. 对频繁修改事件做 150 ms 合并；
3. 同步 `.vpw` manifest；
4. 跟踪编辑器脏状态，区分重载和外部修改冲突；
5. 忽略原子保存临时文件与调试生成的 `wp_metadata_basic.json`。

保存路径涉及磁盘、编辑器缓冲区和 manifest 三种状态。修改此链路时，应验证正常保存、另存为、外部覆盖、外部删除、重命名及未保存冲突。

## 测试与发布边界

本地完整入口为 `test/Run-Tests.ps1`：校验工作流契约，通过 MSBuild 重构建完整 Release 解决方案，并行运行 8 个项目测试，再编译 Inno Setup 安装包，实际安装、首次启动并正常退出、对安装后的 Core/UI 执行扩展真实交互测试，最后卸载和汇总。应用测试保持真实多进程通信，不直接以独立 UI 替代 Core 启动链路。

Pre-merge Check 和 Pre-release 均只允许手动触发。发布流程会对设置新版本后的构建和安装包重新验收，只有选中测试全部执行通过，才同步版本并生成发布草稿；桌面跳过也会阻止发布。详细边界见[测试说明](../test/README.zh-CN.md)与[工作流说明](../.github/workflows/README.zh-CN.md)。
