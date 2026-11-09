# CI/CD 工作流

[English](README.md) | [简体中文](README.zh-CN.md)

仓库包含两个工作流，均仅通过 `workflow_dispatch` 手动触发。提交代码、创建或更新 PR、修改标签以及合并，都不会自动运行工作流。

| 文件 | 手动输入 | 用途 |
| --- | --- | --- |
| `pre-merge-check.yml` | 未合并的 PR 编号；是否包含模型集成测试 | 检查来源与版本标签 → 构建 → 并行测试 → 必需安装冒烟 + 独立真实 UI → 回写 PR 状态 |
| `pre-release.yml` | 已合并的 `dev → main` PR 编号 | 核验 PR → 设置版本并构建 → 并行测试 → 打包/冒烟 + 独立 UI → 汇总检查 → 同步版本 → 草稿 |

两个工作流均拆分为独立的安装冒烟与真实 UI 任务。安装冒烟必须通过：安装 → 打开并正常退出 → 同包覆盖安装 → 原配置/新配置启动并正常退出 → 卸载，不能以跳过代替通过。仅 `test-ui-operation` 使用 job 级 `continue-on-error`，保留入口实际结果与原始报告；汇总通过 `-AllowUIFailure -UIJobResult` 将 UI 失败、超时、报告缺失记录为非阻塞警告。本地默认仍严格拒绝 UI 失败。

## 日常流程

强制安装冒烟还检查内置素材的真实部署、用户修改与删除记录的保留、迁移后保存的库目录、缺失条目的补入及卸载后的数据保留。产物与 Core 测试检查素材清单和媒体解码；独立 UI 增加一次多格式多文件导入，以及每项预处理壁纸的播放检查，其 CI 非阻塞策略不变。详见[测试覆盖](../../test/README.zh-CN.md)。

1. 创建以 `dev` 为目标的功能或修复 PR。
2. 选择 **Actions → Pre-merge Check → Run workflow**，输入 PR 编号。
3. 检查通过后合入 `dev`。
4. 创建 `dev → main` PR，选择一个版本标签，手动运行 **Pre-merge Check**。
5. 检查通过后合入 `main`。
6. 选择 **Actions → Pre-release → Run workflow**，输入该已合并 PR 的编号。
7. 下载并验证发布草稿中的产物，再手动发布。

Run workflow 表单中选择的分支决定使用哪一份工作流定义。输入的 PR 决定被测试或发布的代码，不会用所选分支的 HEAD 替代。建议从默认分支运行。旧的 `ci-check-approval`、`publish-approval` 环境及部署审批已不再使用。

工作流文件必须先进入默认分支，GitHub 才会显示手动运行入口。首次迁移时，需要管理员协调替换必需检查，避免 PR 等待已移除的检查。参阅 [GitHub 手动运行工作流说明](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow)。

## Pre-merge Check

仅支持同一仓库内的 PR：

- 目标为 `main`：来源必须为 `dev`，且四个版本标签中必须恰好选择一个。
- 目标为 `dev`：允许来自 `main`、`dev` 以外的功能或修复分支，不要求版本标签。
- 其他目标分支和 fork PR 会被拒绝。

每次手动运行都会读取当前来源、标签、PR head 和目标分支，并核验 GitHub 的合并快照是否匹配双方当前提交。所有构建与测试均使用该固定快照。

共享构建后，八个项目测试任务并行运行；通过后独立运行两个任务：

1. **Installer Smoke** 编译 ISS，英文安装，启动真实 Core/UI，保存配置和壁纸，并验证正常退出。同包选择中文覆盖安装，验证原配置的语言/偏好/壁纸保留，以及全新配置的中文初始化；每次启动均须正常退出，最后执行真实卸载。保留包体/程序哈希、快捷方式、注册和残留检查。
2. **Real UI Acceptance** 在独立 runner 上使用 Release 输出运行扩展测试，自行拉起真实 Core/UI/播放器并隔离配置、清理会话，不借用冒烟测试的安装包或安装目录。失败为非阻塞警告；无桌面时明确记录 UI 跳过。

有桌面时操作安装、覆盖安装和卸载向导。无桌面时保留静默安装/卸载诊断，但启动/配置检查被跳过将导致两个工作流的冒烟门禁失败；强制清理不能算正常退出。

必需阶段失败会阻止后续阶段，并继续收集报告。项目测试或安装必需阶段的报告缺失、为空、失败、中止或异常仍导致最终检查失败；扩展 UI 失败为非阻塞警告，保留原始错误。七个常规测试项目分别保留各自的 Cobertura 覆盖率报告。

八个项目任务共用 `test/actions/run-project` 和 `test/Run-ProjectTests.ps1`。`test/TestReportSupport.ps1` 负责项目与报告映射，`test/Summarize-TestResults.ps1` 生成汇总。项目任务和汇总使用同一套 TRX 校验逻辑。

`build-output` 保留仓库根目录下的 `src/` 布局和两个测试宿主。冒烟使用该构建打包，独立 UI 直接运行 Release 程序，不重复构建。两份报告分别验证；宿主或清理失败时以真实 UI 入口结果为准，不因报告已写为通过而隐藏失败。

产物传输使用 `actions/upload-artifact@v7.0.2`、`actions/download-artifact@v8.0.2`，均内置 Node.js 24，要求 Actions Runner 至少 `2.327.1`。这与 Web 播放器脚本测试选择的 Node 版本互相独立。上传继续使用 ZIP 及 `overwrite: true`，下载继续解压，并默认拒绝摘要不匹配的产物。参阅上游[上传版本说明](https://github.com/actions/upload-artifact/releases/tag/v7.0.2)、[下载版本说明](https://github.com/actions/download-artifact/releases/tag/v8.0.2)。修改工作流 action 版本后需新建 **Run workflow**，旧流程的重跑保留原工作流版本。

SDK 配置和 NuGet 缓存使用 `actions/setup-dotnet@v6.1.0`、`actions/cache@v6.1.0` 及更新后的 Node.js 24 依赖；请求的 .NET SDK 仍为 `8.0.x`，缓存键和目录不变。`download-artifact@v8.0.2` 解压 ZIP 时仍可能输出 `DEP0005`（`Buffer()`），包括 Smoke 和 UI job。这是[上游已知问题](https://github.com/actions/download-artifact/issues/484)，[修复 PR](https://github.com/actions/download-artifact/pull/490) 尚未合并。继续保留摘要校验及正常解压，不屏蔽 Node 警告，也不引用未合并的 action 构建。

原始报告使用 `test-results-*`，汇总报告使用 `test-summary-*`，因此下载原始报告时不会包含之前的汇总。上传设置 `overwrite: true`，支持在同一次运行中重跑任务。汇总读取各项目指定的报告路径，不递归收集其他包中的报告。

默认排除分类为 `Integration` 的 ML 测试。勾选 **Also run real model inference tests** 可纳入这些测试。常规 ML 测试需要模型文件，文件缺失时明确跳过，不下载模型；这与可选的 UI ModelInference 用例不同，后者缺少模型会失败。本地运行常规模型集成测试：

```powershell
dotnet test src/VirtualPaper.ML.Test/VirtualPaper.ML.Test.csproj --configuration Release --filter "TestCategory=Integration" --settings src/test.runsettings
```

已关闭、已合并或 fork PR 的输入会被拒绝，且不改变之前的检查状态。对于有效的未合并 PR，工作流在其 head SHA 上设置 `pre-merge-check: pending`。写入成功或失败之前，会再次核验 PR 提交、目标和版本标签。状态属于 PR 提交，而非手动运行表单所选分支。参阅 [GitHub commit status API](https://docs.github.com/en/rest/commits/statuses)。

PR 有新提交后需要手动重跑：旧 SHA 的成功状态不能满足新提交的必需检查。修改标签既不会触发运行，也不会使同一 SHA 上已有的成功状态失效，因此修改标签后同样需要手动重跑。发布入口还会再次核验标签。

运行过程中修改 PR、目标提交或版本选择，会阻止检查成功。手动取消可能留下 pending 状态，合并前需重新运行。

## 版本标签

| 标签 | 递增位置，其右侧字段归零 | 从 `0.5.2.0` 递增后的结果 |
| --- | --- | --- |
| `version:major` | 第一位 | `1.0.0.0` |
| `version:minor` | 第二位 | `0.6.0.0` |
| `version:build` | 第三位 | `0.5.3.0` |
| `version:revision` | 第四位 | `0.5.2.1` |

这些标签控制四段产品版本。缺失或存在多个版本标签都会阻止发布；不再根据来源分支决定递增位置。

独立插件更新使用 `update-plugins` 或 `update-plugin-ui/playerweb/scrsaver/ml/shaders`，保留既有插件构建号和兼容客户端的标签格式。带有插件更新标签时，产品版本保持不变，也不创建产品版本同步提交。`dev → main` PR 仍需恰好选择一个四段版本标签。

## Pre-release

仅接受同一仓库内已合并的 `dev → main` PR。其 head 必须有与该 PR 及当前版本标签对应的成功 `pre-merge-check` 状态，且 `main` 仍指向该 PR 的合并提交。未合并、过旧、未经检查或标签不正确的 PR 均会被拒绝。

1. 根据固定合并提交中的 `AssemblyVersion` 和所选标签计算目标产品版本。插件更新保留产品版本。读取已发布组件清单、历史发布标题及插件标签，分配下一个 `YYMMRxx` 构建号；草稿占用的号码也予以保留。构建前检查目标标签和发布是否冲突。
2. 编译前临时更新 `VirtualPaper.csproj`，打包前临时更新 `InnoSetup/setup.iss`，使二进制文件和安装包使用目标版本。
3. 对新构建复用 Pre-merge Check 的八个项目任务/action：产物检查加七个常规测试项目。发布包含 ML 集成测试及 Web 播放器脚本检查，测试任务不重新构建应用。
4. 编译 ISS 并执行必需安装冒烟，通过后生成可选插件补丁。独立的 `test-ui-operation` 对发布构建运行完整真实 UI。`summarize-tests` 下载两类报告，通过 `Summarize-TestResults.ps1 -RequireFullAcceptance -AllowUIFailure -UIJobResult <结果>` 校验：冒烟/项目失败、报告缺失或跳过仍阻塞，仅独立 UI 失败或跳过不阻塞并明确报告。
5. 同步版本前再次检查标签和发布冲突。常规发布仅更新 `main`、`dev` 中的以下字段，再通过 `git push --atomic` 原子推送两个分支。任一更新被拒绝，两个分支均不前进：
   - `src/VirtualPaper/VirtualPaper.csproj`：`AssemblyVersion`
   - `InnoSetup/setup.iss`：`MyAppVersion`、`MyAppVersionText`
   - `README.md`：发布版本徽章
6. 根据 PR 描述创建发布草稿。每次发布都包含安装包和完整的 `app_comp_manifest.json`，插件发布还包含补丁包。标签指向同步后的 `main` 提交；插件更新则指向原始合并提交。最终发布仍需手动执行。

阶段为 `prepare-metadata → build-release → 并行项目测试 → package-release + test-ui-operation → summarize-tests → sync-versions → draft-release`。打包/冒烟与可选 UI 独立运行；同步版本和生成草稿必须同时满足打包成功及汇总通过。插件发布沿用相同门禁但跳过产品版本同步。安装冒烟和选中的发布项目测试不允许跳过，仅独立 UI 可失败或跳过。

一份保留仓库根目录布局的构建产物包含应用/插件输出、全部项目测试及两个测试宿主。并行测试和打包下载同一份构建，只有应用输出进入安装包。组件清单仅生成一次，供发布附件和插件补丁共用。锁屏、修改注册表的 UI 用例及真实空闲屏保测试仍按既有策略排除。

组件清单通过 `GITHUB_TOKEN` 认证获取。网络、权限、下载或解析错误都会停止发布。常规发布会为所有随包插件分配新构建号；插件发布仅更新选定插件，其他条目沿用上一个已发布清单。

如果历史发布没有清单，例如未记录构建号的 `v0.5.2.0`，可通过一次完整发布建立基线。所有随包插件使用新构建号，不猜测旧插件版本。历史标题和标签中的构建号仍占用相应号码。没有历史号码时，从当月 `R01` 开始分配。插件补丁必须有有效的基线清单。已有清单损坏时，不会退回到无基线处理路径。

月内序号固定为两位，达到 `R99` 后停止，避免旧客户端通过字符串比较将 `R100` 误判为更早版本。新月份从 `R01` 开始。构建机器的时钟早于历史编号时也会报错。

版本同步会保留打包期间新增到 `dev` 的业务代码和文档，不会完整合并 `main`、强制重置或覆盖分支。远端并发更新、版本字段不符合预期或推送权限不足，都会阻止同步和草稿创建。`.sln` 不包含产品版本，无需修改。`app_comp_manifest.json` 是生成产物。

若同步成功但创建草稿失败，请在原运行记录中选择 **Re-run failed jobs**。因为 `main` 已前进，重新启动整个工作流会被阻止，以免再次递增版本。同一次运行的产物可覆盖之前上传的内容。清单、发布包和发布测试汇总保留 30 天；构建输出和原始测试报告保留 7 天。

已有标签或不兼容的发布会阻止继续。中断的草稿上传仅在标签、标题和目标提交一致时允许续传。远端检查与写入不是同一个原子事务；准备期间不要手动创建同名标签或发布该草稿。不存在自动强制回滚，也没有单独的手动回滚工作流。

## 仓库配置

预处理后的内置壁纸放在 [`resources/BuiltInWallpapers`](../../resources/BuiltInWallpapers/README.zh-CN.md)。解决方案构建将其复制到 Core 输出的 `BuiltInWallpapers/`，共享构建产物及完整 ISS 安装包使用同一份资源。安装程序向已保存的 `BuiltInWallpapersDir` 添加缺失壁纸目录；旧配置缺少该字段时回退到 `WallpaperDir` 的同级目录，已有目录保持不变。设置页的单一目录入口迁移两个目录并保存两个路径。仅插件更新不部署这些资源；安装验收也通过隔离数据目录验证此步骤。

这些设置必须在 GitHub 上配置，修改本地 YAML 不会改变远端分支规则：

- 为 `main` 要求 PR 和 `pre-merge-check` 状态。若 `dev` 也应仅允许 PR，请配置相同保护。若允许直接推送到 `dev`，推送不会触发这些手动工作流；Pre-merge Check 仍要求一个未合并的 PR。
- 删除 `Check Source Branch`、`ci-check/pre-publish-tests` 等过时的必需检查，新工作流不再提供这些状态。
- 可考虑要求 PR 分支与目标分支保持最新，目标分支变化后重新运行检查。
- `RELEASE_PAT` 必须允许版本同步机器人在仓库规则下更新 `main` 和 `dev`。权限不足时同步失败，工作流不会通过强制推送绕过保护。
- 一次性创建四个固定的 `version:*` 标签。
- 修改本地工作流不会删除远端 `release`、`bugfix` 等旧分支。确认没有未合并工作后再单独清理。

## 本地校验

独立的 Core 入口 MSIX 候选包通过 `.github/scripts/Build-Msix.ps1` 构建，详见 [MSIX 打包](../../Msix/README.zh-CN.md)。两条现有工作流尚未调用此入口。包内容检查不替代必需的 Inno 冒烟及独立 UI 证据，Inno 成功也不表示 MSIX 验收通过。

本地执行对应的完整测试、打包及安装验收：

```powershell
# 管理员 PowerShell 7.2+，干净的 Windows 测试安装环境：
pwsh -NoProfile -File test/Run-Tests.ps1
# 同时选择常规 ML 项目的集成类别：
pwsh -NoProfile -File test/Run-Tests.ps1 -IncludeMLIntegration
```

本地链路使用工作区代码，不校验 PR、不回写状态、不同步版本或创建草稿。完成构建、并行项目测试、ISS 打包与冒烟卸载后，再对 Release 输出运行独立扩展 UI，不下载缺失模型。冒烟失败/跳过及本地 UI 失败仍阻塞；CI 仅在 UI job 与汇总显式允许 UI 失败。

环境要求、单项入口和报告路径见[测试说明](../../test/README.zh-CN.md)，AI 使用方式见[仓库测试 skill](../../.agents/skills/virtualpaper-testing/SKILL.md)。修改测试行为时，应同步更新这些说明及其中英文版本。

只校验工作流与脚本契约，不安装、不操作 UI：

```powershell
pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1
```

该命令校验工作流依赖、共用测试入口、TRX 和覆盖率检查、重试报告隔离、UI 环境跳过、安装包报告、发布元数据和版本同步契约。它使用隔离 Git 仓库和模拟的 GitHub 响应，不会安装应用、锁定会话、修改注册表或向远端推送。
