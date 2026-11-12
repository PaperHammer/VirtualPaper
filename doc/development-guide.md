# 开发与构建

## 环境要求

- Windows 10 2004（Build 19041）或更高版本；建议使用当前受支持的 Windows 11。
- Visual Studio 2022，安装“.NET 桌面开发”和“Windows 应用 SDK / WinUI”相关工作负载。
- .NET 8 SDK。
- 与项目引用匹配的 Windows SDK；当前项目引用 Windows SDK Build Tools `10.0.26100.7175` 和 Windows App SDK `1.8.251106002`。
- 调试 Web 播放相关功能时，需要 WebView2 Runtime。
- 完整安装验收还需要 PowerShell 7.2+、Node.js、Inno Setup 6.7.1；扩展真实 UI 需要对应推理模型及可用桌面，详见[测试说明](../test/README.zh-CN.md)。

首次拉取后应使用 Visual Studio 打开 `src/VirtualPaper.sln` 并还原 NuGet 包。仓库包含 WPF、WinUI、原生 Windows API 和多进程项目，不适合在非 Windows 环境完整构建。

## 构建

完整解决方案应使用 Visual Studio Developer PowerShell 中的 MSBuild：

```powershell
msbuild src/VirtualPaper.sln /t:Build /p:Configuration=Release /p:Platform="Any CPU" /restore /m
```

CI 明确使用 MSBuild，因为 `.sln` 中的 Platform Mapping 会把部分项目路由到 x64；`dotnet build` 不保证应用相同映射。日常只改一个纯库时可以构建单项目，但提交前应按完整命令验证。

## 运行与调试

系统由核心进程和 UI 进程协作组成。Release 版本的 `VirtualPaper.UI` 会检查核心进程是否存在，不能把 UI 当作完全独立的应用启动。常见调试入口：

- 完整程序：先启动 `VirtualPaper`，由 Core 的正式启动路径拉起 `VirtualPaper.UI`。
- WebBackdrop 编辑与预览：使用 `VirtualPaper.UI` 的草稿工作区。
- 安装产物验证：使用 `test/Run-InstallerTests.ps1 -InstallerPath <setup.exe> -RunUIOperationTests`，实际安装、首次启动、执行真实 UI 并卸载；完整链路使用 `test/Run-Tests.ps1`。

实际启动项目、架构（通常为 x64）和打包模式以对应 `.csproj.user`、解决方案配置及本机调试目标为准。

## 测试

完整本地验收请在干净 Windows 测试环境，以管理员身份打开 PowerShell 7.2+ 后，从仓库根目录执行：

```powershell
pwsh -NoProfile -File test/Run-Tests.ps1
# 额外选择常规 ML 集成测试：
pwsh -NoProfile -File test/Run-Tests.ps1 -IncludeMLIntegration
```

流程为工作流契约校验、完整 Release 重构建、8 个并行项目测试、ISS 打包、安装、首次启动和正常退出、安装后的扩展真实 UI、卸载及报告汇总。失败会阻止后续阶段，安装后的清理仍会执行。已有日常安装时入口会拒绝运行；不要绕过保护或未经授权删除用户数据。

单项开发可在完整 Release 构建后运行 `test/Run-ProjectTests.ps1 -JobId test-core`，或通过 `test/Run-UIOperationTests.ps1 -Configuration Release -Module GeneralSettings` 测试一个真实 UI 模块。真实 WinUI 宿主位于独立的 `test` 目录，不使用 `dotnet test` 启动。无可用桌面时真实 UI 明确跳过，不视为 UI 验收通过。

准确的项目清单、模块范围、模型要求、结果判定和报告路径见[测试说明](../test/README.zh-CN.md)。AI 请阅读 [AGENTS.md](../AGENTS.md) 和[仓库测试 skill](../.agents/skills/testing/SKILL.md)。两条 CI 流程均为手动触发，详细规则见[工作流说明](../.github/workflows/README.zh-CN.md)。

## 推荐阅读顺序

1. 根目录 `README.md`：确认产品能力。
2. `VirtualPaper.Models` 与 `VirtualPaper.Common`：理解共享模型、常量和基础设施。
3. `VirtualPaper/App.xaml.cs`：理解后台核心服务。
4. `VirtualPaper.UI/App.xaml.cs` 和 `MainWindow.xaml.cs`：理解前端组合方式。
5. `Workloads.Entry`：理解文件加载和创作运行时选择。
6. 按任务进入 `StaticImg`、`WebBackdrop`、各 Panel 或 Player 项目。

## 修改检查清单

- 是否跨越进程边界，需要同步客户端、服务端或模型契约？
- 是否新增了需要在组合根注册的服务或 ViewModel？
- 是否涉及文件类型，需要更新识别、加载器和运行时映射？
- 是否修改文件保存，需要覆盖外部修改与未保存冲突？
- 是否修改 WinUI 资源，已确认资源字典和输出复制规则？
- 是否有对应单元测试，且完整解决方案仍可由 MSBuild 构建？

## 工作区注意事项

`bin/`、`obj/` 和自动生成的临时 `.csproj` 不应作为源码阅读入口。WebBackdrop 内置的 Monaco Editor 是第三方分发文件，业务修改通常应集中在包装控件、HTML bridge 和项目源码中，不直接编辑 `Assets/monaco-editor-*`。
