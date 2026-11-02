# WinUI 操作测试

`VirtualPaper.UIOperationTest` 是独立的未打包 WinUI 可执行测试项目，本身承担宿主职责。
组件用例通过项目引用加载生产页面；应用用例启动隔离的真实 Core，并由 Core 拉起真实 UI。
两类用例都不依赖 Sandbox，也不连接用户日常运行的 VirtualPaper 实例。

```text
test/
  Run-UIOperationTests.ps1
  VirtualPaper.UIOperationTest/
    App.xaml / App.xaml.cs         UI 线程、用例执行和 JSON 结果
    Cases/                        按生产模块组织真实 UI 交互用例
      Components/GeneralEffectTests.cs
      Application/NavigationSettingsTests.cs  真实 Core + UI 的设置保存与重开验证
    Infrastructure/
      UiTestContext.cs            页面生命周期、等待、UI Automation 操作和临时目录
      AppProcessSession.cs        真实进程、RPC 就绪、UI 重启及退出
```

## 运行

在具有图形会话的 Windows 桌面中使用 PowerShell 7.2 或更高版本：

```powershell
./test/Run-UIOperationTests.ps1 -Configuration Release
```

默认使用 Visual Studio MSBuild 构建完整解决方案，再启动测试；已构建时可以加 `-NoBuild`。
默认从 `src/VirtualPaper/bin/<Configuration>/net8.0-windows10.0.19041.0` 启动 Core，
也可用 `-CoreExe` 指定另一个完整构建中的 Core（同目录必须有生产 UI 插件）。通过 `-ReportPath` 指定新的 JSON
结果路径，默认写入系统临时目录。脚本检查进程退出码、计划用例数及结果完整性；失败时抛出错误，
超时会终止本次启动的宿主及其子进程。不要用 `dotnet test` 运行这个 WinUI 可执行项目。
报告旁的 `.artifacts` 目录保留隔离配置和前后台日志，临时运行目录会清理。

## 当前覆盖

组件交互：普通图片和深度图片分别执行以下链路：

1. 在真实 WinUI 窗口中导航到生产 `GeneralEffect` 页面，检查对应的特效分组。
2. 通过 UI Automation 的 ExpandCollapse 打开选项，RangeValue 修改已加载滑块，检查事件到达。
3. 通过按钮 Invoke 恢复默认，重复五轮，检查值恢复、控件不累计或脱离页面。
4. 保存修改，检查真实 JSON 落盘及应用回调；重建页面并展开选项，确认控件回显已保存值。

应用交互：`Application.NavigationSettings.SaveAndReopen` 执行以下链路：

1. 启动真实 `VirtualPaper.exe`，以带超时的设置 RPC 等待后台就绪。
2. 通过生产 ShowUI RPC 拉起真实 `VirtualPaper.UI.exe`，仅识别该 Core 的直接子进程。
3. 通过跨进程 UI Automation 进入设置并关闭导航动画，不直接调用 ViewModel 或 SetSettings。
4. 独立读取后台 RPC 和真实 JSON，确认修改已保存。
5. 正常关闭 UI，再由 Core 拉起新的 UI 进程，验证开关回显为关闭。
6. 正常关闭 UI 和 Core，检查退出码。JSON 记录 Core/UI PID、保存值及正常退出结果。

`Passed` 表示本用例的交互与保存断言通过，不能代替整套应用健康检查。
`Evidence.RuntimeErrors` 单独保留运行中出现的 ERROR 日志；脚本会提示，不能因退出码为 0 就忽略。

脚本通过仅对子进程生效的环境变量传递测试会话 GUID 和数据根目录。
Common 在进程初始化时读取它们，隔离配置、日志、Core/UI 单例、管道及共享文件锁。
显式测试会话中跳过开机自启注册、启动通知及待安装插件更新；设置保存仍使用真实生产代码。
未设置环境变量时保持正常运行方式。测试不读写用户壁纸库或生产配置。

组件用例在 UI 线程串行运行；跨进程 UI Automation 在单独 MTA 线程执行，参见
[Microsoft UI Automation 线程说明](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading)。
新增模块时在 `Cases` 中实现用例，并在 `App.xaml.cs` 注册；脚本及 CI 不需要维护用例名称或数量。
组件用例的效果服务和应用回调使用记录实现；应用设置用例不替代前后台服务。
本轮未启动壁纸播放器，不覆盖系统级鼠标键盘输入、桌面挂载、WebView 渲染画质或 AI 推理。

现有七个常规测试项目保持原目录；`Core.Test/T_Workflows` 继续负责文件、服务和 gRPC
业务集成测试，不作为真实 UI 交互用例迁入本项目。

CI 顺序为：构建 → 七项常规测试并行 → WinUI 操作测试 → 汇总。WinUI 失败、未执行或结果缺失
都不能让最终 CI 检查通过。
进入 WinUI 阶段前还会检查常规 TRX 的未处理异常，防止“断言全通过、testhost 仍崩溃”的假绿。
