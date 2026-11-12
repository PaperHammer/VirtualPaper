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

## 主 UI 硬件加速策略

`UiHardwareAccelerationEnabled` 默认开启，通过现有设置服务持久化。RPC 使用可选字段：旧响应缺少字段时按开启处理，旧客户端保存时省略字段则保留服务端已有值。`VirtualPaper.UI` 启动后创建唯一的 `UiHardwareAccelerationPolicy`，固定本次 UI 会话的偏好；性能页只更新保存值，并在它与启动值不同时提示下次启动主界面生效。

`UiWebViewEnvironment` 统一创建 UI 使用的 WebView2 环境并复用同一分区的初始化任务。UI 数据目录位于原 WebView2 根目录的 `UI/GPU` 或 `UI/Software` 下，Markdown 预览再使用独立子分区。软件模式增加 `--disable-gpu` 启动参数；按模式隔离目录可以避免重开 UI 时旧浏览器进程尚未退出造成选项冲突。桌面播放器与屏保不消费 UI 策略，继续使用原有目录。这里目前仅接入 UI WebView；AI 推理和绘画设备将在各自模块后续接入同一策略。

## 主 UI 界面动画

主 UI 的“性能 → 界面动画”统一控制导航、主题切换、壁纸卡片、智能演示及接入共享样式的控件过渡。沿用 `NavigationAnimationsEnabled` JSON/RPC 字段以保留旧偏好；界面和 ViewModel 使用 `InterfaceAnimationsEnabled` 表达扩大后的范围。`UiAnimationPolicy` 合并应用偏好与 Windows 动画设置，`UiAnimation` 将系统通知调度回 UI 线程，关闭 UI 时解除监听。

需要动画控制的组件显式实现 `IUiAnimationController.SetAnimationsEnabled(bool)`。`UiAnimation.Attach(this)` 仅绑定该组件的 Loaded/Unloaded：加载时订阅策略并立即应用当前值，卸载时释放订阅。策略变化只通知已注册组件，不扫描页面、不反射查找子控件，也不为普通页面或窗口宿主安装通用行为。`ArcWindow` 按窗口关闭生命周期释放订阅。

组件自己停止、恢复动画并呈现最终状态：壁纸库维护当前已实现卡片的动画对象，对比滑块和智能演示管理自身时间线。`ArcExpander` 位于 `VirtualPaper.UIComponent.Layout`，XAML 统一使用 `layout:ArcExpander`。它保留 WinUI 原生模板与交互，只访问自己明确的模板根、标题和箭头；`ArcContentDialog` 只适配自身的 Container 模板根。二者使用的状态管理器仅处理当前组件自身的有限过渡，不递归查找内容区；持续加载反馈不受影响。Expander 复用静态箭头，卸载只清理模板接入和菜单租约，不重新启用箭头动画；重新加载时再应用当前策略。它显式管理自己的菜单；共享菜单在最后一个使用者释放后恢复原值。新增含动画的组件应实现接口并管理自己的动画、菜单与卸载清理。

动画偏好与硬件加速偏好互相独立，动画设置立即生效；WinUI 合成器继续使用框架的硬件加速能力。关闭产品中的硬件加速不会强制 WinUI 合成器改用软件渲染，也不会关闭界面动画。

## 文件变化与保存

共享的 `VirtualPaper.Common.Utils.ProjectSystem` 提供文件系统监听和文档状态跟踪。WebBackdrop 在其上增加 `ProjectFileManager`：

1. 监听项目目录中的创建、修改、删除和重命名；
2. 对频繁修改事件做 150 ms 合并；
3. 同步 `.vpw` manifest；
4. 跟踪编辑器脏状态，区分重载和外部修改冲突；
5. 忽略原子保存临时文件与调试生成的 `wp_metadata_basic.json`。

保存路径涉及磁盘、编辑器缓冲区和 manifest 三种状态。修改此链路时，应验证正常保存、另存为、外部覆盖、外部删除、重命名及未保存冲突。

## 壁纸元数据路径

壁纸包内的 `wp_metadata_basic.json`、`wp_metadata_runtime.json` 从文件格式版本 19 起保存相对于各自 JSON 文件的路径。例如基本元数据的 `FolderPath` 为 `.`，`FilePath` 为 `assets/image.png`；位于显示器/运行类型子目录中的运行元数据可通过 `../..` 引用壁纸目录。读取统一使用 `WpBasicData.Load/LoadAsync`、`WpRuntimeData.Load/LoadAsync`，在内存中还原为绝对路径，供 UI、RPC 和播放器使用；不要直接反序列化后把相对路径交给文件 API。旧版绝对路径仍可读取，再次保存时写入新格式。

壁纸库右键“升级”会将格式 18 的基本元数据及各显示器/运行类型的运行元数据原地转换为 19，保留 UID、素材、缩略图、深度图和效果文件，不重新导入媒体或改写 Web 工程清单。解析或保存失败会返回错误；正在播放或预览的壁纸仍按现有规则拒绝升级。

版本分流由 Core 的 `WallpaperMetadataMigrator` 集中处理，`WallpaperControl` 只负责调用和保留低于 18 的旧版重建入口。迁移按版本逐步执行，当前为 `18 → 19`；当前格式直接返回，非法版本和高于程序支持范围的版本会报错。后续格式变更需增加明确的迁移步骤，不将未知版本当作兼容格式。迁移保存显式指定该步骤的目标版本，先保存运行元数据，最后更新基本元数据；保存成功后才更新内存版本。写入中断可能留下部分已升级的运行元数据，此时基本版本仍为旧版，再次升级可继续执行。

设置中的壁纸库根目录和全局显示器布局记录属于本机状态，保留实际路径。临时编辑器/预览文件若跨磁盘引用外部素材，Windows 无法表示为相对路径，因此保留外部绝对引用；完整壁纸目录内部的路径均使用相对路径。Web 工程的 `project.json` 入口和预览图字段本身已是相对路径。

## 测试与发布边界

本地完整入口为 `test/Run-Tests.ps1`：校验工作流契约，通过 MSBuild 重构建完整 Release 解决方案，并行运行 8 个项目测试，再编译 Inno Setup 安装包，英文安装、首次启动并保留配置和壁纸、同包中文覆盖安装、验证原配置保留及全新配置中文初始化，再对安装后的 Core/UI 执行一次扩展真实交互测试，最后卸载和汇总。应用测试保持真实多进程通信，不直接以独立 UI 替代 Core 启动链路。

Pre-merge Check 和 Pre-release 均只允许手动触发。发布流程会对设置新版本后的构建和安装包重新验收，只有选中测试全部执行通过，才同步版本并生成发布草稿；桌面跳过也会阻止发布。详细边界见[测试说明](../test/README.zh-CN.md)与[工作流说明](../.github/workflows/README.zh-CN.md)。
