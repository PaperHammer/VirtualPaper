# 测试与本地完整验收

[English](README.md) | [简体中文](README.zh-CN.md)

本地测试链路由 MSTest 项目测试、真实安装包验收和 WinUI 交互测试组成。完整运行使用 `Run-Tests.ps1`，下面的单项入口复用其中各阶段。

`VirtualPaper.UIOperationTest` 是独立、非打包的 WinUI 可执行程序，同时充当测试宿主。组件用例通过项目引用加载正式页面；应用用例启动隔离的真实 Core 进程，再由 Core 拉起真实 UI。两者都不使用 Sandbox，也不会连接用户正在运行的 VirtualPaper 实例。

## 目录结构

```text
test/
  Run-Tests.ps1                      完整本地 Release / 安装包验收
  Run-ProjectTests.ps1               运行一个已构建的项目测试
  Run-InstallerTests.ps1             真实安装、启动、UI、卸载
  Run-UIOperationTests.ps1
  Summarize-TestResults.ps1          校验原始报告与覆盖率
  VirtualPaper.InstallerTest/        安装向导和首次启动验证
  VirtualPaper.UIOperationTest/
    App.xaml / App.xaml.cs          UI 线程、用例注册、JSON 结果
    Cases/
      Components/                   正式页面的交互测试
      Application/                  按模块划分的真实 Core + UI 测试
    Infrastructure/
      UiTestContext.cs              页面生命周期、等待、UI Automation、临时文件
      AppProcessSession.cs          进程、RPC 就绪、UI 重启、关闭
      ApplicationUi.cs              跨进程控件查找与交互
```

## 运行测试

命令均从仓库根目录执行。AI 应先阅读 [AGENTS.md](../AGENTS.md) 和 [VirtualPaper 测试 skill](../.agents/skills/testing/SKILL.md)，了解测试选择、进程隔离、结果判定及工作流门禁。

| 目标 | 入口 | 范围 |
| --- | --- | --- |
| 完整本地验收 | `test/Run-Tests.ps1` | 重新构建 Release、并行项目测试、ISS/安装冒烟、独立扩展 UI |
| 单个项目回归 | `test/Run-ProjectTests.ps1 -JobId <id>` | 使用已有 Release 产物，不构建 |
| 真实 UI 模块或测试集 | `test/Run-UIOperationTests.ps1` | 默认先构建解决方案，`-NoBuild` 跳过构建 |
| 已有安装包 | `test/Run-InstallerTests.ps1 -InstallerPath <setup.exe>` | 必需安装冒烟：真实安装/覆盖安装、启动/正常退出、语言/数据保留及卸载 |
| MSIX 包内容 | `test/Test-MsixPackage.ps1 -PackagePath <application.msix>` | 检查实际包，不执行安装或 UI 验收，详见 [MSIX 打包](../Msix/README.zh-CN.md)。 |
| MSIX 完整验收 | `test/Run-MsixTests.ps1` | Release 重建、八组项目测试、签名测试包、真实首次启动／扩展 UI／重装／数据保留／卸载；需要管理员和未锁定桌面 |
| 工作流与脚本契约 | `.github/scripts/validate-test-workflow.ps1` | 隔离校验，不安装、不执行真实 UI |

MSIX 使用独立完整入口：在未锁定 Windows 桌面以管理员执行 `pwsh -NoProfile -File test/Run-MsixTests.ps1`。流程创建本轮独有的测试包身份和临时签名证书，Release 重建后执行八组项目测试及脚本检查（不允许跳过），构建／检查自包含 MSIX，再安装到当前用户。复用现有扩展 UI 套件，真实走 Shell AUMID 激活 → Core → 生产 UI／播放器；原生 API 校验 Core、UI 及重启 UI 的包身份。必需阶段还包括首次自动打开 UI、真实导入、同包重装、偏好／导入卡片／内置壁纸保留、新配置启动和卸载。重装前后的保留配置与扩展 UI 配置分别隔离。只清理本轮测试包和证书，原始报告及隔离配置保存在新 `TestResults/msix-acceptance-*` 目录。UI 失败、缺失或桌面不可用均不能通过；强制清理进程不能算正常退出。WACK、跨版本升级、干净机器运行依赖、持久开机启动／系统设置仍需单独发布验收。现有两条 GitHub 工作流继续只验收 Inno。

Inno 本地完整验收使用与 CI 一致的阶段。请以**管理员身份**打开 PowerShell 7.2 或更高版本后运行：

```powershell
./test/Run-Tests.ps1
# 额外执行可选的 ML 项目集成测试：
./test/Run-Tests.ps1 -IncludeMLIntegration
```

此入口完整重建 **Release**，并行运行八个项目测试，编译 ISS，再执行必需安装冒烟：英文安装与首启 → 中文同包覆盖安装 → 原配置/新配置启动验证 → 卸载。冒烟完成后，再对 Release 输出运行独立的 **Extended** 真实 UI，自行拉起 Core/UI 和隔离配置。本地任一阶段失败均阻止后续执行；尝试安装后仍执行卸载清理。每次写入新的 `TestResults/local-acceptance-*`。需要 Visual Studio MSBuild、.NET 8、Node.js、Inno Setup 6.7.1；可通过 `-InnoSetupCompiler <ISCC.exe>` 指定编译器。

检测到已有 Virtual Paper 安装或开始菜单快捷方式时会拒绝运行。应先明确卸载日常版本并保留用户数据，或使用干净测试机。安装和卸载会修改系统安装注册信息与快捷方式；UI 用例仍不执行启动项注册、锁屏等持久系统设置操作。无桌面时独立 UI 明确跳过，但冒烟因必需启动/配置检查未执行而失败。

`-IncludeMLIntegration` 只控制常规 ML 项目的可选集成类别，不控制独立 UI 测试集：完整本地入口始终选择 **Extended**，包含真实推理和深度渲染。即使未传该开关，扩展 UI 也需要对应模型文件。

完整链路如下：

```mermaid
flowchart LR
    A[工作流契约] --> B[Release 重构建]
    B --> C[8 个并行项目测试]
    C --> D[ISS 打包]
    D --> E[英文安装与首次启动]
    E --> R[中文覆盖安装及配置验证]
    R --> U[卸载 - 冒烟完成]
    U --> F[独立扩展 UI]
    F --> G[汇总]
```

本地失败阻止后续阶段，尝试安装后仍执行清理；真实 UI 在本地顺序运行。CI 固定 PR/版本化源码，先并行生成选中的包，再运行一轮八组共享项目测试。全部通过后，EXE/MSIX 验收子流程在两台独立 hosted Windows VM 上并行，每路内部先冒烟卸载，再重新安装运行扩展 UI 并卸载。所有选中的检查（含扩展 UI）必须无跳过通过。本地不回写 PR 状态或同步版本，详见[工作流说明](../.github/workflows/README.zh-CN.md)。

安装验收使用较短的隔离临时配置路径，避免超出 Inno Setup 6 的路径限制。验收结束后复制到安装报告旁的 `.artifacts/profiles` 目录，再清理临时目录。

可用 `-ResultsRoot <新目录>` 指定完整链路的报告根目录。其中保留 `build.log`、`workflow-contracts.log`、8 份 `test-*.log`、`installer-build.log`、`installer-smoke.log`、`ui-acceptance.log`、本轮 `installer-package` 和原始 `test-results-*` 报告。失败报告也会保留。按发布要求校验一轮已完成的结果：

```powershell
./test/Summarize-TestResults.ps1 -Root '<本轮结果目录>' -RequireFullAcceptance
```

这只校验已有证据，不会补跑测试或准备发布；桌面不可用导致跳过，或选中的项目测试存在跳过，都会使严格校验失败。

### 单个项目测试

先通过 Visual Studio MSBuild 构建完整 Release 解决方案，再使用新的报告目录：

```powershell
msbuild src/VirtualPaper.sln /t:Build /p:Configuration=Release '/p:Platform=Any CPU' /restore /m
./test/Run-ProjectTests.ps1 -JobId test-core -ResultsDirectory "TestResults/core-review-$([guid]::NewGuid().ToString('N'))"
```

请在 Developer PowerShell 中使用 `msbuild`。应保留解决方案的平台映射；单独以不同架构构建宿主可能留下不匹配的依赖副本。

| Job ID | 项目 |
| --- | --- |
| `test-artifacts` | `VirtualPaper.ReleaseBuildData.Test` |
| `test-core` | `VirtualPaper.Core.Test` |
| `test-ui-logic` | `VirtualPaper.UI.Test` |
| `test-images` | `StaticImg.Test` |
| `test-web-backdrop` | `WebBackdrop.Test` |
| `test-ui-components` | `VirtualPaper.UIComponent.Test` |
| `test-ml` | `VirtualPaper.ML.Test` |
| `test-shader` | `VirtualPaper.Shader.Test` |

准确映射以 `TestReportSupport.ps1` 中的 `Get-ProjectTestSpecs` 为准。CI 和完整本地入口还会随 UI 逻辑测试执行 `node .github/scripts/test-atmosphere.cjs`、`node .github/scripts/test-depth-player.cjs`；单独运行项目入口不会执行这两项脚本检查。

### 单个真实 UI 模块或测试集

以下独立 UI 入口用于模块开发，不替代安装包完整验收：

在具有图形会话的 Windows 桌面上，使用 PowerShell 7.2 或更高版本：

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release
# 完整回归：额外运行真实推理、反复 UI 生命周期和 3D 渲染测试。
./test/Run-UIOperationTests.ps1 -Configuration Release -Suite Extended
# 运行单个应用模块。默认测试集不包含可选集成用例。
./test/Run-UIOperationTests.ps1 -Configuration Release -Module WallpaperLibrary
# 只运行一个具体用例，不重复整个模块：
./test/Run-UIOperationTests.ps1 -Configuration Release -NoBuild -Module WallpaperLibrary.ImportMultipleFormatsAndReopen
# 安装目录中必须已有 Real-ESRGAN 模型，不会自动下载。
./test/Run-UIOperationTests.ps1 -Configuration Release -Module ModelInference -TimeoutSeconds 900
```

运行脚本会先通过 Visual Studio MSBuild 构建完整解决方案，再启动测试。二进制文件已准备好时可使用 `-NoBuild`。默认从 `src/VirtualPaper/bin/<Configuration>/net8.0-windows10.0.19041.0` 加载 Core，也可通过 `-CoreExe` 指定另一份完整构建，其中必须包含正式 UI 插件。

通过 `-ReportPath` 指定新的 JSON 报告路径，默认位于系统临时目录。已有报告或产物路径会被拒绝，防止复用旧结果。脚本检查退出码、预期用例数和报告完整性；失败会抛出错误，超时则终止本次启动的宿主及子进程。**不要使用 `dotnet test` 运行此 WinUI 可执行程序。**

默认总超时为 180 秒，`-Suite Extended` 为 900 秒。可按需将 `-TimeoutSeconds` 设置为 30 至 900。报告旁的 `.artifacts` 目录会保留隔离设置和 Core/UI 日志，临时运行目录会被清理。

标准测试集包含 20 个用例。扩展测试集共 23 个用例，在此基础上增加真实模型推理与队列取消、12 轮 UI 打开关闭及屏保页面通知，以及深度推理和渲染帧暂停、恢复检查。内置 3D 壁纸用例需要构建中的 Depth Anything 模型来校验缓存键，但不需要重新推理。扩展集的 Real-ESRGAN 和 Depth Anything 模型也必须预先安装；缺失模型会使验收失败，不会静默缩减覆盖范围。CI 运行扩展测试集。真实空闲屏保测试仍需显式选择；锁屏和修改注册表仍不纳入测试。

### 没有可用桌面的机器

构建或启动测试进程之前，只读预检会检查 Windows 交互会话、当前输入桌面的访问权限、物理或虚拟显示器边界，以及壁纸验收所需的 Explorer 桌面。

环境不适用时，生成包含 `Status: Skipped`、`Passed: false`、`ExpectedCount: 0` 及原因的报告，并以退出码 0 结束。原因包括 `WindowsRequired`、`NoInteractiveDesktop`、`NoDisplay` 和 `NoDesktopShell`。详细信息写入 `.artifacts/environment.json`。

无可用桌面时 UI 写入明确的跳过报告。选中的 CI 安装冒烟/UI 均拒绝跳过；本地严格验收也拒绝失败、超时和报告缺失。桌面检测不修改会话、注册表或显示配置。

## 默认覆盖范围

### 下载与插件替换

Core 测试通过隔离的本地 HTTP 服务验证单／多文件下载的字节及进度、HTTP 失败、暂停恢复、取消后复用、校验文件响应及依赖容器释放。插件安装测试使用独立应用目录、真实 ZIP 和 SHA256 校验，覆盖替换成功、验证失败、备份不完整、部分替换或清单更新后回滚、新增／移除插件、恢复失败保留备份、取消及提交后清理失败。断言比较原始文件内容和清单；插件进程控制使用 mock，不操作已安装应用或真实 UI。

### 图片工程持久化

图片工程测试使用离屏 Win2D 画布，验证多图层 VPD 保存重开、像素字节、显隐／顺序、选中图层和调色板数据，以及 v1 编码保留与 v2 升级。覆盖业务数据／图层增量保存、目标文件锁定、原文件和缓存不变、临时文件清理、恢复图层另存为新文件，以及写入失败后保持未保存状态；图层删除、旋转经撤销／重做后再次保存重开。全程不打开编辑器窗口或文件选择器。

### 语言即时切换

源文案只维护在 `localization/Strings` 中；正常构建解决方案时，根据共用语言清单及映射生成 UI/Core/Common 资源。包体测试使用内嵌源文案与映射快照，逐一核验清单内语种的包内生成 key 及 value，不依赖资源注释。修改方法及单独生成、校验命令见[语言资源说明](../localization/README.zh-CN.md)。

`Localization.LiveText.RefreshAndPreserveState` 在正式组件上遍历清单内全部语种，最后恢复英文；预期文案独立读取目标语种资源，不以当前 localizer 的结果自证。检查画笔名称、预设风格、无名称提示、队列文字及窗口标题，并保留画笔参数、风格选择、输入路径和自定义名称。`Application.GeneralSettings.SaveLanguageThemeAndReopen` 还通过真实设置界面、Core RPC、JSON、UI 进程不变、完整导航悬浮提示及新增语种重开检查持久化和热切换。

`Localization.LongText.IntelligentLayouts` 在德语、法语和俄语下，用亮／暗主题和宽／窄窗口检查正式介绍页、两种新建任务表单及完成任务卡片；断言预览比例、图文相对位置、表单上下排列、控件边界、按钮点击面积、操作保持同一行及文字未截断，并恢复测试宿主窗口大小。这些用例需授权后实际运行；编译通过不代表视觉验收通过。显示／文本缩放仍需下述桌面检查。

真实智能配置用例还会在这三种语言、两种宽度下检查正式遮罩和底部按钮；现有扩展推理用例利用同一个真实推理结果，分别打开三种语言的关闭提醒，检查文字、复选框及操作按钮边界，取消关闭并保留会话，最后恢复英文，不重复进行推理。

桌面排版验收还应切换德语、法语和俄语，缩放窗口，并检查较高显示／文本缩放下的表现。重点包括侧栏文字、两种介绍页和智能任务表单、风格库、任务操作按钮及关闭提醒。侧栏保持紧凑，文字可读，悬停可查看完整名称；介绍页预览保持原比例，宽页面图文左右排列，窄页面上下居中排列。任务卡片的信息、状态和按钮始终排在同一行；窗口变窄时对长文件路径使用省略号，不将操作按钮移到下一行。长文案可换行，窄窗口下表单上下排列，正文可滚动且底部按钮保持可见。项目单测和资源完整性检查不代表视觉验收通过；仅在允许占用前台时执行这项检查。

### 组件效果

`GeneralEffect.RImage.EditRestoreSaveAndReopen` 和 `GeneralEffect.RImage3D.EditRestoreSaveAndReopen` 覆盖普通图片与深度图：

1. 在真实 WinUI 窗口中导航到正式的 `GeneralEffect` 页面，检查对应效果分组。
2. 通过 UI Automation ExpandCollapse 展开选项，通过 RangeValue 修改已加载的滑块，检查事件传递。
3. 调用恢复操作，重复五轮，检查数值、控件数量和页面挂载状态。
4. 保存并核验真实 JSON 和应用回调。重新创建页面，展开选项，检查已保存值是否正确显示。

### 导航设置

`Application.NavigationSettings.SaveAndReopen`：

1. 启动真实 `VirtualPaper.exe`，通过带超时的设置 RPC 等待就绪。
2. 使用正式 ShowUI RPC 启动 `VirtualPaper.UI.exe`，仅接受当前 Core 的直接子进程。
3. 通过跨进程 UI Automation 导航并关闭导航动画，不直接调用 ViewModel 或 SetSettings。
4. 独立检查后台 RPC 和持久化 JSON。
5. 正常关闭 UI，请求 Core 启动另一个 UI 进程，检查开关仍为关闭。
6. 正常关闭 UI、Core 并检查退出码，记录 PID、持久化值和关闭结果。

重新打开时会等待真实窗口就绪。若操作系统已报告进程退出，但 Core 的退出回调尚未完成，脚本会有限重试幂等的 ShowUI 命令。`ShowUiRequests` 记录这些尝试；仅收到 RPC 响应并不代表窗口已经就绪。

### 性能设置

`Application.PerformanceSettings.SaveAndReopen` 检查七项设置：全屏、焦点、电池、节能、远程桌面场景下的播放策略，多显示器检测，以及仅桌面播放声音。

每项都修改真实控件，通过 Core RPC 和持久化 JSON 确认，再重新打开 UI 核验。这验证设置持久化，不模拟电池或 RDP 状态，也不验证真实壁纸暂停、静音行为。

### 壁纸库

`Application.WallpaperLibrary.ImportApplyCloseDelete`：

1. 通过真实添加按钮和系统选择器导入隔离 PNG，检查元数据、缩略图和源文件哈希。
   重新打开格式 18、绝对路径的样本，通过真实右键菜单升级，检查格式 19、相对存储路径、UID 保持不变以及源文件像素未变。
2. 搜索不匹配内容，再清空搜索恢复条目。普通图片初始标题可能为空，因此这一步不验证非空标题的匹配。
3. 聚焦卡片，通过 Shift+F10 打开正式菜单，取消删除并检查文件仍在，然后作为普通图片应用。
4. 检查布局 JSON、由 Core 直接创建的 PlayerWeb PID，以及播放器实际的 WorkerW 父窗口。
5. 重新打开 UI，检查条目仍存在。通过显示器控件关闭壁纸，检查空布局和播放器退出。
6. 确认删除，检查磁盘与列表中的条目被移除，再重新打开 UI 确认壁纸库为空。源 PNG 必须保持不变。

卡片容器提供基于壁纸 UID 的 AutomationId。键盘和鼠标菜单使用同一正式处理逻辑。原生输入仅限测试拥有的窗口，临时置顶状态会恢复，不会关闭其他桌面应用。

真实选择器必须成功提交，不替换存储服务。菜单查找失败时，会在产物中保留所属窗口截图。清理会等待 WebView2 释放延迟占用的缓存句柄。仅凭桌面挂载和退出断言，不能确认 3D 渲染质量或暂停正确性；这些由 Rendering 模块单独覆盖。

`Application.WallpaperLibrary.ImportMultipleFormatsAndReopen` 在**同一个真实系统选择框中一次提交**这组素材的图片、GIF、视频和 Web 压缩包。检查每项文件及缩略图、唯一标识、媒体类型、源文件哈希保持不变，并重开 UI 检查所有卡片仍存在。

`Application.WallpaperLibrary.BuiltInsApplyRenderAndClose` 在 Core 启动前将处理好的素材准备到隔离内置库中。通过真实壁纸库发现全部卡片，逐项应用，检查 WorkerW 挂载与非空渲染截图，GIF／视频／Web 还要求实际像素随时间变化。通过显示器关闭控件检查布局清空及所属播放器退出；播放器当前实现会主动终止其进程。另将一张内置图片按 3D 应用，检查预生成深度缓存得到保留。这里准备的文件不能证明 ISS 安装成功，实际部署由独立的强制安装冒烟验收验证。

原有单图片导入／升级／删除，以及真实深度推理／暂停恢复用例继续保留；预处理素材不能替代这些不同链路。播放检查复用已有选择器、壁纸及截图助手。

壁纸整理逻辑由 `WallpaperOrganizationTests`、`WpSettingsViewModelTests` 和 `LibraryContentsViewModelTests` 单独覆盖：标题/描述/作者/原有及个人标签的全库搜索，收藏/类型/标签组合筛选，排序后分页，跳过缺失元数据及丢弃过期查询。标签输入经过短暂等待后按包含关系匹配，忽略大小写和首尾空白，并建议已有的原有及个人标签，无需先选择建议。测试覆盖部分输入、空输入、无匹配，以及标签列表刷新时保留输入。整理信息以壁纸 UID 为键，独立存入应用数据目录下的 `wallpaper_library.json`。测试检查批量标签、并发保存、保存失败时不发布修改、损坏配置保留，以及重启、内置元数据更新和库目录迁移后保留整理信息，不修改壁纸原始元数据。多选操作作用于已加载的卡片。这些单元测试不证明新增工具栏、收藏按钮或标签弹窗的真实交互；真实 UI 验收需明确授权。

单张壁纸通过“编辑信息”弹窗修改标题、描述、作者和个人标签。壁纸自带标签只读展示；收藏保留在卡片和菜单中，保存表单不会覆盖当前收藏状态。多选保留批量标签操作。右键菜单打开前明确记录操作对象；收藏入口仅在能改变对象状态时显示，与卡片星标读取同一份持久化状态。逻辑测试覆盖已选/未选卡片的右键操作对象、统一/混合收藏状态、保存后搜索索引刷新、自带标签和当前收藏状态保留、只改个人标签时元数据不变、元数据保存失败，以及个人整理保存失败后的重试。元数据和个人整理分开保存：后者失败时，前者已保存的内容仍保留且可搜索。鼠标/键盘打开菜单、弹窗布局、取消操作及实际星标更新仍需真实 UI 验收。

作者和个人标签输入后按回车添加为可逐项移除的条目。`WallpaperInfoTokensTests` 覆盖已有分号列表、空值/重复输入、删除、标签粘贴、作者姓名中的逗号，以及保存/重试时包含尚未回车的输入。元数据保存测试还检查多作者持久化和作者搜索。编辑表单复用弹窗已有滚动区，不再嵌套滚动控件；实际对齐、条目换行以及回车不触发弹窗保存仍需真实 UI 验证。

壁纸库输入查询等待 180 ms，合并连续输入；过期查询保留当前卡片且不访问索引。翻页复用当前筛选、排序结果。批量导入统一发布成功项并刷新一次，后续条目失败或取消时也会发布此前的成功项。数据更新保留已加载条数，不主动重置滚动；筛选条件变化时回到第一页。逻辑测试覆盖批量发布、空批次、保留分页，以及导入与等待中的筛选交错执行。元数据修改、库重建及个人整理保存会使相关查询和标签缓存失效，逻辑测试覆盖这些路径及目录迁移。GridView 自行管理滚动视口，以便回收屏幕外卡片。卡片操作按钮停留 120 ms 后展示；离开、滚动、回收或进入多选模式时取消待展示操作，并停止、隐藏当前动画。悬浮只更新对应卡片。视口虚拟化、自动分页和悬浮时序仍需真实 UI 验证，逻辑测试不代表实际帧率或内存表现。

预览资源清理由无界面逻辑检查补充覆盖：`WebViewScriptExecutorTests` 验证事件顺序、突发状态更新按键合并、重复释放，以及调度回调始终未完成时两个后台循环仍可退出；`WallpaperThumbnailTests` 检查逐帧释放临时转换缓冲后 GIF/视频的帧内容，以及重复成功、保存失败路径中的真实进程 GDI 对象数。转换缓冲逐帧释放，GIF 编码器仍会保留输出帧直到保存。这些检查不创建 WebView2 控件，也不证明原生预览窗口已正确关闭；WebView/服务初始化中关闭窗口，以及时间感知任务清理，仍需真实 UI 验收。

壁纸库工具栏的“更多”菜单打开单页效果预设弹窗：左侧为有界、虚拟化的预设列表，右侧显示所选项的名称、类型和效果参数。内置项仅标记“内置”，不可编辑、改名或删除。选中内置项后在“新建”右侧显示简短的只读提示；未修改的用户预设不显示状态提示。只读时仅禁用数值控件的外层宿主，滚动和分组详情按钮保持可用。内置项的分组摘要完整换行，氛围/视差详情可展开查看。真实 UI 验收需检查滚动、详情展开、参数不可修改，以及复制后和语言切换后的编辑状态。“壁纸类型”表示预设兼容范围，不触发应用预设。“新建（默认值）”和“基于所选新建”创建独立草稿并取消左侧选中；复制使用已保存的源参数、新 ID 和副本名称，不修改原项。保存成功后重新选中已保存项。用户预设在同一编辑区修改名称和效果。内置项仅包含所有类型都支持的公共色彩/氛围参数，应用时保留壁纸原有的缩放和视差设置；复制内置项后选择图片、视频或 3D 类型，可包含这些类型专属参数。“更多”保留从壁纸新建和放弃修改；从壁纸新建为可取消的步骤，仅存在多份配置时显示配置选择。底部原生按钮最小宽度仍为 128。

仅用户实际修改名称、类型或效果后，才在“新建/基于所选新建”右侧显示未保存状态；修改回原值仍保留提示，不与原数据比较。仅新建、复制或从壁纸创建草稿不触发未保存提示，但仍可直接保存或放弃该新草稿。悬停状态可查看切换或关闭将丢弃修改的完整说明。工具栏高度固定，内置只读、未保存和保存中状态共用同一位置，不改变弹窗高度。未保存草稿采用原生 InfoBar 的警告背景色，适配亮暗主题；只读/保存中状态使用信息背景色。保存成功或选择未修改的用户项后隐藏状态。切换、新建/复制、放弃修改、关闭及 Escape 均直接执行，不弹出离开确认，也不自动保存。保存校验或写入失败时保留草稿并显示错误；仅写入期间暂时阻止关闭。删除按钮通过附着在按钮上的 Flyout 二次确认，并显示目标名称；取消、Escape 或点击外部均不删除。确认时重新核对捕获的 ID 与当前用户预设，切换草稿或关闭时隐藏浮层并清空目标。加载预设不等待壁纸索引，也不阻止关闭；关闭时取消来源读取、清空列表并释放唯一的效果编辑器。列表和配置名称采用编译绑定。选中切换、内置只读、草稿取消选中、工具栏状态的亮暗配色及弹窗高度稳定、未修改新草稿的直接保存、修改标记持续保留、保存/放弃/关闭、删除确认/取消及语言热切换仍需授权后真实 UI 验收。壁纸效果面板继续在内存中试用兼容预设，点击“应用并关闭”才保存到壁纸，“取消预设预览”恢复此前参数。

`EffectPresetTests`、`EffectPresetStoreTests` 覆盖支持类型间的兼容组合、内置项保留缩放/视差/播放设置、排除资源路径及控件定义、捕获/应用快照独立、缺失属性定义补全、氛围效果与样式联动、参数类型/范围、名称长度及不同区域设置下的校验。下拉索引必须为控件可读取的 JSON 整数，拒绝小数及指数写法。持久化检查覆盖改名/复制/删除、内置保护及独立副本的编辑保存、多存储实例并发保存/改名/删除、重名/重复 ID、非法身份/参数、空值/缺失/损坏/未来版本目录、删除最后一项、读取中的旧快照及原子替换失败后的重试；拒绝操作时保留原文件。用户预设位于应用数据目录的 `effect_presets.json`，复用现有原子文件写入。这些是无界面测试；单页布局、只读控件及工具栏未保存状态、实际播放器回退、布局和语言热切换仍需授权后进行真实 UI 验收。

真实 UI 的 `GeneralEffect.RImage/RImage3D.EditRestoreSaveAndReopen` 用例还检查：创建参数控件和重新打开下拉列表后保留预设选择、数值发送到效果服务、手动修改后显示 Custom，以及取消时还原首次试用前的参数且不写入壁纸配置。目录刷新后，仅改名保留选中状态；参数变化则切为 Custom，保留当前效果，重新选择同一预设时应用新参数，并继续保留最初的取消快照。无界面测试覆盖参数比较及独立存储实例重载，真实 UI 用例通过隔离的用户预设检查下拉框行为。控件初始化、重复的数值通知和已移除控件的回调不能算作手动修改。管理面板使用同步名称变化通知，让载入名称始终处于更新保护范围内；真实验收还需检查打开未修改的用户预设不显示未保存提示，修改名称、类型或参数后才显示持续的警告状态。预览下拉框里的 Custom 仅描述手动参数状态，不作为可选预设。

`WallpaperIndexEntryTests` 覆盖预设来源名称的回退顺序：标题 → 媒体文件名 → 壁纸 ID，包括空标题、纯空白标题，以及目录有名称时仍使用 ID 回退。列表按显示名称排序，从该壁纸创建预设时也使用同一个默认名称。文件名由现有索引读取流程顺带保留，不额外读取元数据，也不修改壁纸原始标题。选择器的实际显示仍需真实 UI 验收。

预设来源选择器顶部固定搜索和收藏筛选，下方有界列表保留虚拟化，左侧缩略图、右侧名称和作者。每次打开只读取一份索引/收藏快照并排序一次；搜索匹配显示名称和作者，采用 150 ms 防抖，筛选复用已排序快照，不重复读盘或查询整理数据。缩略图随现有索引读取，解码宽度限制为 144，随实际创建的列表项加载，不使用共享图片缓存；弹层隐藏后清空列表快照并取消待完成加载，容器和图片复用交给原生虚拟化。管理弹窗关闭时也取消来源读取、停止并解除搜索计时器订阅。空结果仍保留已选壁纸及运行配置。`WallpaperSourceQueryTests` 覆盖匹配规则、排除其他元数据、空结果、稳定排序、持久化收藏、10,000 项重复搜索不再读取收藏，以及重新打开更新收藏状态。这些是无界面检查；原生滚动/图片回收、内存占用、选择保留，以及直接切换/关闭、工具栏未保存状态（含加载或保存失败、Escape）仍需授权后真实 UI 验收。

壁纸库工具栏左侧保留搜索、筛选和排序，右侧显示带文字的“添加壁纸、屏幕控制、更多”。“仅看收藏”位于筛选内，并计入按钮上的已启用条件数量；“更多”包含批量选择、高清适屏和效果预设。多选模式切换为批量工具和“完成”按钮；窄窗口下普通操作区另起一行。逻辑测试覆盖组合筛选计数及清空后的恢复。实际布局、菜单入口和模式切换仍需真实 UI 验收。

升级拦截依据 Core 中实际运行的播放器，不再把保存的恢复布局当成运行状态。`WallpaperControl_LayoutTests` 和 `LibraryContentsViewModelTests` 覆盖有旧布局但无播放器、有播放器但无保存布局、播放器已退出及无法读取运行状态。`AspectRatioUtilTests` 覆盖依据保存的分辨率显示旧 `FUnknown` 宽高比（含视频帧率后缀）、非法尺寸、常见比例名称及保留有效的原宽高比。`GeneralInfoViewModelTests` 检查实际详情面板的显示且不改写元数据。运行类型弹窗左侧选中标记和完整 Upgrade Basic 交互仍需真实 UI 验收。

Upgrade Basic 在格式已是当前版本时也会重新读取图片/GIF/视频的派生属性。`WallpaperMetadataRefreshTests` 检查 18/19 版本的空白/错误属性修复、第二次请求重新读取已变化媒体、保留 ID、创建时间、用户文案、媒体、缩略图和效果，以及无法读取图片时不写入零尺寸。已安装的 Web 项目保留清单及已知的原压缩包大小，分辨率/宽高比标为不适用。实际详情面板使用公共宽高比格式化方法。非常规比例在括号中附上最接近的常用比例，例如 `729:512 (≈ 3:2)`；竖图匹配对应竖向比例，常用比例保持简洁，提示文本不写入元数据。gRPC 补齐创建时间（统一为 UTC）及订阅状态；实际消息序列化往返和列表刷新测试验证更新卡片不移位、不删除后重插、不重置滚动，并兼容不含创建时间的旧响应。默认“最新优先”时新导入项仍在最前；指定标题/作者排序时继续遵循相应排序字段。

### 屏保设置

`Application.ScreenSaver.SettingsAndWhitelist`：

1. 在真实页面中启用屏保，修改等待时间和效果，再独立核验 Core RPC 和 JSON。
2. 通过正式的异步 `IStoragePicker` 选择系统 `notepad.exe`，加入白名单但不启动它。
3. 检查名称、完整路径和已保存图标，测试匹配及不匹配搜索，重新打开 UI 检查持久化。
4. 移除条目和图标，关闭屏保，重新打开 UI 确认关闭状态及空列表。

启用时先等待设置保存完成，再发送启动 RPC。白名单路径通过 gRPC 往返传递，通知管道带有隔离会话后缀。

选择器提交使用异步点击，目标是所属选择器内经过验证的原生 Button，通过类名和原生 ID 识别。不发送全局 Enter，也避免另存为校验期间的同步 UIA/COM 错误。此用例使用较长的空闲超时并关闭锁屏，不验证真实空闲激活、屏保进程启动或系统锁定。

### 高清适屏

`Application.ScreenFit.PreviewCancelApplyAndReopen`：

1. 从“更多 → 高清适屏”进入，通过系统选择器选取竖图，等待示例预览消失，切换 contain/cover，检查可见区域说明。
2. 取消并检查没有创建壁纸、布局或播放器；重新打开后必须重新选择源图。
3. 关闭增强并选择 contain 后应用，检查源文件哈希、目标显示器，以及三个效果 JSON 中的 Scaling。
4. 检查 PlayerWeb 挂载在 WorkerW 下，再重新打开 UI，核验壁纸库条目、当前预览和持久化设置。
5. 通过正式 UI 关闭播放器并删除测试数据，检查源文件未变，Core/UI 正常关闭。

此用例不运行超分，不验证推理、预览像素质量、显示器切换或 cover 模式的真实渲染。

### 壁纸效果

`Application.WallpaperEffects.EditRestoreSaveAndReopen` 使用真实 Core、UI、播放器和效果服务：

1. 导入并应用图片，从显示器控件打开正式调整窗口。
2. 修改氛围和强度，恢复已保存值，然后选择 Snow，强度设为 71。
3. 检查保存前应用配置未变；保存后核验磁盘中的效果、风格和强度，同时默认模板不变。
4. 重新打开 UI 和调整窗口，检查三个控件，再关闭播放器并删除测试数据。

修改通过正式 RPC/IPC 传递，播放器保持运行。此用例不检查渲染器状态或像素，也不能替代粒子质量、深度模型、鼠标视差或暂停恢复检查。

### 常规设置

`Application.GeneralSettings.SaveLanguageThemeAndReopen`：

1. 切换亮色、深色主题，修改背景材质、自启动偏好和语言，通过 Core RPC 和 JSON 检查每次变更。
2. 在 General、System、Other 页面之间导航，核验设置保留及 About 图片的无障碍名称。
3. 修改材质后，观察 Core 自动重启 UI；中英文切换保持同一个 UI 进程，检查页签和材质选项立即更新，无需重启。
4. 关闭并重新打开 UI，检查中文标签、材质选项、语言和自启动偏好，再恢复英文并关闭自启动偏好。

显式测试会话跳过系统自启动注册。此用例不验证真实注册、材质像素、About 图片解码或更新下载。

### 主窗口与托盘主题

`Application.ThemeSync.MainWindowAndTray` 通过真实主题按钮依次切换亮色 → 深色 → 跟随系统 → 亮色 → 深色，检查 RPC 和 JSON 持久化，采集真实主窗口与所属 Core 托盘菜单，并核验渲染背景亮度。重新打开 UI 后再次检查深色。跟随系统仅对照当前系统偏好，不修改系统主题或注册表。

同一用例还会在亮色、深色下打开并取消 AI 新任务，检查按钮的无障碍名称、键盘焦点与点击区域，并保留截图。测试会话的托盘图标带有会话 ID，避免误点其他正在运行的实例。

### 日志导出

`Application.SystemSettings.ExportLogsAndCancel` 先取消正式保存选择器，再将 ZIP 导出到用例临时目录。等待压缩包完全可读后，将设置与会话 JSON 对照，检查存在布局数据、Core/UI 日志非空且归档条目路径安全。随后重新打开 UI 并检查正常关闭。

仅导出隔离会话的诊断信息，不导出正式用户日志，也不打开后台调试窗口。

### 静态编辑器

`Application.StaticEditor.RotateCancelSaveReopenAndExport`：

1. 通过正式入口和选择器打开隔离 PNG，将当前默认 1920×1080 画布旋转为 1080×1920。
2. 取消未保存关闭提示，确认 UI 仍打开。取消一次设计保存，再保存为 `.vpd`。
3. 使用正式文件头读取器读取尺寸和图层数。重新打开 UI，加载 `.vpd` 并检查画布。
4. 通过正式菜单导出 PNG，检查尺寸、图像内容非空、源文件哈希和正常关闭。

使用真实 Win2D 和着色器资源，不覆盖画笔、图层重命名、跨 DPI 行为和逐像素精确输出。

### Web 编辑器

`Application.WebEditor.CreateFilesSaveReopenAndExport`：

1. 从正式模板创建隔离项目，新建文件夹，取消一次新建文件，再创建、删除文件。
2. 通过文件树打开入口文件，等待 Monaco 当前文件状态就绪。发送真实键盘输入前，确认原生焦点属于浏览器。
3. 通过正式菜单保存，独立读取磁盘中的编辑标记；仅凭未保存指示消失不能判定成功。
4. 重新打开 UI，通过系统选择器加载 `.vpw`，检查文件树和入口文件，导出完整 ZIP，核验编辑内容、清单和被删除文件。
5. 编辑但不保存，先取消项目另存为，再选择空目标目录。核验副本包含未保存编辑和空资源目录，原项目字节不变，编辑器切换到副本。
6. 继续修改并保存副本，导出后分别重新打开两份项目，检查新清单名称和原文件内容。

上下文菜单和键盘输入仅作用于所属窗口，并检查前台和遮挡。不使用 DOM 注入、测试专用保存入口或服务替身。不覆盖预览质量、全部编辑器命令、OCR 和像素级渲染。`WebProjectCopyTests` 单独覆盖完整复制、文本编码、原项目保留、清单编辑、目标拒绝、开始前取消、准备未保存编辑时取消、源文件被独占锁定后的失败清理，以及壁纸包和完整工程 ZIP 的实际内容。ViewModel 测试覆盖缓存淘汰后重新打开文件，以及单文件另存为保留原文件未保存状态；它们不能替代完整编辑器会话切换的验收。`FileUtilTests` 另验证旧复制重载的覆盖和递归行为。

### 编辑未保存内容时退出

`Application.EditorExit.ImageCancelSaveAndExit` 与 `Application.EditorExit.WebCancelSaveAndDiscard` 让真实图片、Web 编辑器保持未保存状态，取消窗口关闭提示，再通过正式 RPC（图片）或真实托盘退出入口（Web）请求 Core 退出。必须出现真实保存提示；取消后检查 UI 与 Core 仍可使用。

图片用例还会取消保存选择器，再次退出时保存 `.vpd`，核验编辑后的画布尺寸。Web 用例打开当前测试会话的真实托盘菜单，选择退出并放弃修改，检查入口文件未变。两者都要求 UI、Core 正常退出、源文件不变且没有 ERROR 日志。取消的 Core 退出请求结束后，才发起下一次请求。按钮查找限定在 ContentDialog 内，避免把取消按钮与 TabView 的关闭按钮混淆。

### 智能任务配置

`Application.Intelligent.ConfigureCancelAndReopen`：

1. 取消正式图片选择器，检查提交仍为禁用。
2. 选择 256×144 图片，在 2×、4× 和恢复之间切换，检查 512×288、1024×576 和原尺寸说明。
3. 取消并重新打开，确认源图和倍率重置。风格迁移中仅选择风格图，检查添加源图之前提交始终禁用。
4. 重复取消和重新打开，检查可用性、源文件哈希未变及正常关闭。

此用例不提交推理任务，也不下载模型。报告明确记录 `ModelInferenceAndQueueTested=false`。推理、队列、运行中任务取消及结果导入导出，需要下面的可选用例。

## 可选集成用例

这些用例不在默认测试集中。可显式选择模块，或通过 `-Suite Extended` 纳入模型推理、屏保生命周期和渲染用例。真实空闲屏保必须显式选择模块运行。

### 模型推理与队列

`Application.ModelInference.SuperResolutionQueueCancelSaveAndImport`：

1. 通过正式 UI，使用已安装的 Real-ESRGAN ONNX 模型提交三个任务。
2. 确认第三个任务排队、第二个正在运行，取消这两个任务，让第一个完成真实推理。
3. 通过系统保存选择器导出 PNG，检查 2× 尺寸，通过正式按钮导入，并比较导出、导入哈希。
4. 删除已完成任务，重新打开 UI，核验壁纸库条目、源文件未变、正常关闭且没有 ERROR 日志。

同一用例还会在推理进行时取消托盘退出，切换到其他页面后检查窗口关闭，并检查临时、已导出、已入库标记。取消退出或保存选择器会保留结果及提醒设置；删除临时结果需要确认。第二次真实推理验证确认关闭后保存“不再提示”、重新打开 UI，以及在设置中重新开启提醒。未完成的任务始终需要确认，不受该设置影响。

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release -Module ModelInference -TimeoutSeconds 900
```

缺少模型会失败，不自动下载，也不静默跳过。它验证 CPU ONNX 推理和基本队列行为，不验证主观画质、全部模型或长时间压力。

### 屏保生命周期

`Application.ScreenSaverLifecycle.NavigateNotifyCloseAndReopen` 在一个真实 Core 下启动并正常关闭 12 轮 UI。每轮两次访问屏保页面，通过真实会话管道接收设置通知，并由 UI 日志确认收到。它检查监听恢复和关闭，不验证真实空闲或锁屏。

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release -Module ScreenSaverLifecycle -TimeoutSeconds 300
```

UI 关闭超过 10 秒时，会保留可见控件信息和所属窗口截图。用例仍失败，不延长超时掩盖问题。

### 渲染

`Application.Rendering.DepthInferenceDrawPauseAndResume` 使用真实选择器、3D 类型选择和已安装的 Depth Anything V2。检查深度缓存，启用自动镜头移动，通过正式截图 RPC 捕获 WebView 像素。断言覆盖测试图颜色、运动帧差异、暂停稳定性和恢复运动。

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release -Module Rendering -TimeoutSeconds 300
```

仅从测试拥有的播放器采集截图，保留在报告的 `logs/UIOperation/rendering` 下。模型缺失会失败，不下载模型。此用例验证基本渲染正确性，不验证主观画质、多 GPU、多显示器或第三方 Web 壁纸的暂停支持。

### 真实空闲屏保

`Application.ScreenSaverIdle.RealIdleInputWakeAndRepeat` 等待一分钟真实空闲，检查屏保加载，通过鼠标输入唤醒，确认正常退出，再重复一轮。

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release -Module ScreenSaverIdle -TimeoutSeconds 300
```

禁用始终转发移动输入，但屏保活动通知仍必须正常工作。用例使用屏保的可见 HWND，并考虑隐藏 WPF 所有者窗口。每轮最多等待两分钟。真实键鼠活动会重置计时器；报告记录观察到的系统空闲秒数，便于识别干扰。仅运行不锁屏的屏保，跳过锁屏、自启动注册和持久化系统设置写入。

## 隔离、结果与限制

Core 元数据测试覆盖基本/运行 JSON 的相对路径、整个壁纸目录搬迁、旧绝对路径兼容以及可选空路径。运行时使用解析后的绝对路径，打包应保留完整目录及内部相对结构。UI 用例从原始 JSON 读取路径后，以该 JSON 所在目录独立解析，再验证实际文件。

迁移测试覆盖旧版重建分流、当前版本不改写、非法/未来版本拒绝、写入前校验运行元数据版本、取消，以及部分迁移后的重试。写入失败不能提前更新内存中的元数据版本。

`Passed` 仅表示用例声明的交互和持久化断言通过，不代表整个应用健康。环境跳过不算通过。`Evidence.RuntimeErrors` 单独保留 ERROR 日志，即使退出码为 0，脚本仍会提示这些错误。失败用例会在后续进程轮次可能轮转日志前，快照保存 Core/UI 日志和配置。

仅向子进程注入的环境变量携带测试会话 GUID 和数据根目录。Common 初始化时读取它们，隔离设置、日志、Core/UI 单例、管道和共享文件锁。进程测试**不启用 `Constants.IsTestMode`**，着色器路径、消息和桌面挂载检查保持正式行为。常规单元测试仍可使用 `IsTestMode`，数据隔离不依赖该标记。

显式会话跳过自启动注册、启动通知和待执行插件更新，但仍通过正式代码保存设置。真实锁屏和修改注册表的用例按项目测试约定**跳过**，不注册，也不请求许可。进程用例保持屏保锁定关闭，并跳过带 `SPIF_UPDATEINIFILE` 的壁纸刷新，因为它会持久化系统设置。`SkippedSystemOperations` 记录这些限制，不将其计为通过。不设置这些环境变量时，应用行为不变。测试不读写用户壁纸库或正式配置。

组件用例在 UI 线程串行运行。跨进程 UI Automation 在独立 MTA 线程执行，参阅 [Microsoft UI Automation 线程指南](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading)。组件用例使用记录调用的效果服务和应用回调；应用用例使用真实后台。壁纸库、适屏和效果用例启动真实播放器，并在各自声明的范围内核验桌面挂载。

默认测试集不验证 WebView 渲染质量、多显示器布局、视频播放策略或 AI 推理。七个常规测试项目保持现有位置。`Core.Test/T_Workflows` 继续覆盖文件、服务和 gRPC 集成，不迁入 UI 宿主。

## 安装冒烟与独立 UI 验收

CI SDK 配置、NuGet 缓存及产物上传/下载 action 内置 Node.js 24，要求 Actions Runner 至少 `2.327.1`，与本地/Web 脚本的 Node.js 设置独立。版本及摘要校验说明见[工作流指南](../.github/workflows/README.zh-CN.md)。

所选包全部成功后，CI 的八组共享项目测试只运行一轮，内部并行。全部通过后，`test-exe-acceptance`、`test-msix-acceptance` 调用同一个 `package-acceptance.yml` 模板，分别使用全新的 GitHub-hosted Windows VM。每路内部顺序执行冒烟卸载 → 独立重装并扩展 UI → 卸载；清理失败阻止 UI。消费既有渠道产物并核对源码 SHA/哈希，选中渠道的原始冒烟/UI 报告仍必须齐全（每渠道两份）。同一 Windows 桌面上的两个 runner 进程不满足隔离要求。本地完整入口仍先完成冒烟并卸载，再运行独立 Release UI。

`Run-InstallerTests.ps1` 只对一个真实安装包、一个独有临时安装目录执行必需冒烟：

1. 英文安装。安装后的 Core 走正式首启路径打开 UI，通过 UI、RPC 和持久化设置验证英文。保存主题/动画偏好，导入真实 PNG，正常退出 UI/Core，保留隔离配置和壁纸哈希。
2. 同包选择中文覆盖本轮安装，检查包体、程序哈希、版本、注册、快捷方式和语言标记。
3. 通过 Core 正式 ShowUI RPC 打开原配置，验证原英文、偏好及壁纸文件/UID 保留；另用空配置验证中文初始化。每次均须正常退出。
4. 前面失败仍尝试真实卸载，检查进程、文件、快捷方式和注册残留；强制清理只是恢复，不能算正常退出通过。

版本 3 `InstallerSmoke` 报告包含 Install、Open、Reinstall、VerifyPreserved、OpenFresh、Uninstall。两个工作流均要求**全部通过且无跳过**。无桌面时可保留静默安装/覆盖安装/卸载诊断，但启动/配置跳过会导致冒烟失败。`-InstallerLanguage chinese` 可反转语言顺序。可选 `-InstallerLanguage french` 验证法语初始化、英文覆盖安装、保留原法语配置和数据、全新配置初始化英文，继续要求真实向导及全部生命周期阶段；CI 默认仍为英文→中文。

冒烟会真实修改安装文件、系统卸载注册及快捷方式，须在干净 CI 或明确准备好的本地管理员会话执行；保留已有安装/AppId/快捷方式保护及用户数据。它与禁止持久系统设置操作的独立 UI 用例分开。

```powershell
# 干净 Windows 测试机上的必需冒烟：
./test/Run-InstallerTests.ps1 -InstallerPath <installer.exe> -ReportPath TestResults/Installer/installer-results.json
# 可选反向语言顺序：
./test/Run-InstallerTests.ps1 -InstallerPath <installer.exe> -InstallerLanguage chinese -ReportPath TestResults/InstallerChinese/installer-results.json
# 独立 UI，直接使用完整 Release 输出，无需安装：
./test/Run-UIOperationTests.ps1 -Configuration Release -NoBuild -Suite Extended
# 仅契约校验，不安装、不启动 UI、不写注册：
./.github/scripts/validate-test-workflow.ps1
# 有桌面时可单独验证临时向导 fixture：
./test/Test-InstallerWizard.ps1
```

CI 使用 `Summarize-PipelineTests.ps1`：共享 TRX/覆盖率、选中的包、对应子流程结果及原始冒烟/UI 报告均须通过。报告缺失、清理失败、桌面验收跳过或源码/包哈希变化均阻止 PR 成功与版本/草稿写入。版本同步契约使用隔离本地 Git 仓库，验证 main 单次版本提交、dev 真实合并祖先关系、并行开发保留、重跑幂等、文本/二进制/删除/重命名冲突采用 main、dev 不冲突块保留及原子推送拒绝。旧 `Summarize-TestResults.ps1 -AllowUIFailure` 仅保留诊断兼容用途，两条 CI 均不调用。渠道选择、正式商店变量和重试见[工作流说明](../.github/workflows/README.zh-CN.md)。

工作流契约包含隔离 Git 测试，覆盖仅在 `main` 生成版本提交、合回 `dev`、保留新增开发内容、冲突清理、重跑、下一次发布及原子推送被拒绝。测试仅使用本地 fixture 远端，不向 GitHub 推送。

打包汇总契约还会拒绝安装包哈希／版本不符、MSIX 源代码版本与包身份不符、配置验收报告缺失，以及内置壁纸证据不完整。两个渠道的完整安装 UI 验收均要求七个模块齐全，即使精简后的报告声称全部预期用例通过也不能放行。反例完成后会恢复并重新验证通过样本；这些契约不会真实安装程序或操作 UI。

## 添加覆盖

源文案和语言清单位于 [`localization`](../localization/README.zh-CN.md)。`Test-LocalizationResources.ps1` 随工作流契约运行，不操作 UI 或安装程序。Core 单测覆盖语言匹配、旧／新安装标记、首启首选语言顺序、升级保留及中性／卫星资源回退；产物测试逐项比对清单内语种和内嵌文案快照，MSIX 包体校验同时检查语种声明与实际翻译文件。

EXE/MSIX 启动探针从目标语种资源读取“通用”页签的预期文字，不依赖测试宿主的界面语言；产物测试将共用读取逻辑与各语种内嵌源文案比对。法语作为可选代表验收入口，不代表全部语种的安装向导已经验收。

`LocalizationResourceSupport.ps1` 逐项校验源文案／XAML 属性及 Core、Common 的中性和卫星资源，包括 UI 内 Common 副本；它在产物测试前、EXE 安装／覆盖安装后及最终 MSIX ZIP 条目上运行。`Test-LocalizationResourceSupport.ps1` 覆盖缺文件、缺失／过期文案、重复 XML key 和退役属性；`-PayloadDirectory <Release-bin>` 还读取实际构建 DLL，并检查正常／损坏的归档夹具，全程不安装、不操作 UI。Core 单测另覆盖安装语言标记被独占锁定后，按系统首选语言回退并持久化的行为。

内置壁纸文件夹维护在 [`resources/BuiltInWallpapers`](../resources/BuiltInWallpapers/README.zh-CN.md)。构建时带入 Core 输出，安装器读取 `BuiltInWallpapersDir`；旧配置缺少该字段时回退到 `WallpaperDir` 的同级目录，添加缺失文件夹并保留已有目录及用户修改。安装验收向安装程序传递隔离数据目录，不写入日常壁纸库。Core 和 UI 逻辑测试覆盖旧配置默认值、独立路径经 RPC 保存及重启恢复、双目录扫描/删除、预览配置落盘、单一入口下的双目录迁移、索引刷新及失败时保留原数据。

产物测试嵌入源元数据清单，构建漏掉文件夹或残留旧条目都会失败。Core 流程测试搬到新目录后解码实际素材（含动态帧和预生成深度图），与用户条目共同扫描，并检查原始素材及用户数据没有变化。真实 UI 按上文验证多文件导入和预处理条目播放。

安装冒烟先验证初次完整部署，再移动隔离内置库、修改已有条目标题、添加用户文件、删除已有说明文件，并移走另一整项。真实覆盖安装必须遵循保存的目录，完整保留已修改文件夹（包括用户删除的文件），并补入缺失条目；卸载必须保留该库。检查位于强制冒烟阶段，报告中的 `BuiltInWallpapers` 保存对应证据。`Test-BuiltInWallpaperTestSupport.ps1` 不安装程序，仅检查校验器的正反例，不模拟或替代真实 ISS 验收。

在 `Cases` 下实现新用例，并在 `App.xaml.cs` 中注册；运行脚本和 CI 不维护硬编码用例名称或数量。

- 共用测试基础设施与最小真实场景单独提交。
- 后续每次提交增加一个模块，包含必要的共用辅助代码。
- 进入下一模块前，核验真实交互、后台持久化、重新打开后的状态和进程清理。
- 在报告中说明未覆盖行为和已知错误，跨模块修复单独提交。
- 不将报告、运行日志或无关本地修改与功能一起提交。
