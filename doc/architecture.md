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

`UiWebViewEnvironment` 统一创建 UI 使用的 WebView2 环境并复用同一分区的初始化任务。UI 数据目录位于原 WebView2 根目录的 `UI/GPU` 或 `UI/Software` 下，Markdown 预览再使用独立子分区。软件模式增加 `--disable-gpu` 启动参数；按模式隔离目录可以避免重开 UI 时旧浏览器进程尚未退出造成选项冲突。桌面播放器与屏保不消费 UI 策略，继续使用原有目录。绘画设备适配仍属于后续工作。

主 UI 的依赖注入从同一启动策略创建 `InferenceDevicePolicies`，分别管理超分辨率、风格迁移和深度估计。关闭加速时，保留原来的 CPU 会话、任务与分块并发，不展示推理后端或处理档位。开启表示允许受支持的功能使用 GPU，由 `InferenceDevicePreference` 定义当前模型的默认偏好：单次、最长边约 518 且生成后释放的深度流程优先 CPU；当前风格迁移优先 CPU；最小输入块也为 512×512 的超分优先 GPU。GPU 会话禁用内存模式并采用顺序执行，同一个会话的完整模型调用串行化，超分 Tile 也不并发使用该会话。不同模型的会话独立。设备只在创建会话时选择，已加载模型继续复用，不逐图切换，不同时常驻两套后端，也不在启动时在线跑分。`LoadModel/Run`、现有工厂和 RPC 不变；当前以实际张量及执行流程制定模型偏好，后续扩大输入规格或加入批处理时再用实测校准策略。这里改变执行设备，不会自动提高模型精度或图片质量。

深度模型通过单例 `IDepthEstimateFactory` 创建，由生成流程的 `using` 释放；根 DI 容器不再解析并长期跟踪可释放的瞬态模型。每个实例仍使用共享的深度设备策略。`DepthMapCache` 只缓存最近一个模型的 SHA-256，按完整路径、长度、最后写入时间和创建时间失效；缓存访问在现有生成门控内串行执行，文件读锁阻止计算期间写入/替换。源图片仍每次计算内容哈希，生成文件名和原有缓存键保持兼容。受管理的模型文件更新后需改变文件指纹；同时刻意保留全部元数据的外部字节改写不属于此缓存的失效保证。

当前内置 AdaIN 图与原生 ONNX Runtime 1.20.0／1.23.2 的 DirectML Reshape 路径已验证不兼容，固定形状及图优化设置不能消除错误。`AdaInModelCompatibility` 仅对该图 SHA-256 和实际原生运行库版本组合（通过 OrtEnv 查询，不能使用 C# 绑定程序集版本）直接选择 CPU，省去首次 GPU 失败；CPU-only 会话保留 CPU 并发；主动优选 CPU 或已知不兼容均显示 CPU 处理。在 GPU 偏好下，其他图或运行库版本继续尝试 GPU。兼容限制不修改全局设置，也不禁用其他模型的 GPU 策略。

`OnnxInferenceSession` 在初始化时只针对明确的 DirectML／设备故障创建 CPU 会话；执行时还处理 ONNX 原生 `RuntimeException`（算子兼容错误可能没有设备名），释放旧会话并完整重试本次模型调用一次。取消、明确的无效参数／模型错误和普通文件错误继续向调用方传播；CPU 重试仍失败则直接返回错误，不再次重试。每种模型的 GPU 故障记录保留到本次 UI 退出，避免重复尝试故障设备。`InferenceState` 分开记录全局许可、实际设备和选择原因（关闭、CPU 偏好、模型不支持、GPU 偏好或 GPU 故障）；只有 GPU 故障才标记 CPU 回退。任务卡片通过阶段订阅累计 CPU／GPU，混合任务显示 CPU + GPU，并保留故障回退说明；关闭硬件加速时隐藏设备状态，阶段结束解除订阅并保留结果状态。风格迁移的后续超分失败只重试超分，不重做已经完成的风格迁移。档位和不同精度模型后续单独适配。

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
