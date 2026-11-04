# CI/CD 工作流

[English](README.md) | [简体中文](README.zh-CN.md)

仓库包含两个工作流，均仅通过 `workflow_dispatch` 手动触发。提交代码、创建或更新 PR、修改标签以及合并，都不会自动运行工作流。

| 文件 | 手动输入 | 用途 |
| --- | --- | --- |
| `pre-merge-check.yml` | 未合并的 PR 编号；是否包含模型集成测试 | 检查来源与版本标签 → 构建 → 并行测试 → ISS → 安装/启动/UI/卸载 → 回写 PR 状态 |
| `pre-release.yml` | 已合并的 `dev → main` PR 编号 | 核验 PR → 设置版本并构建 → 并行测试 → 打包/安装/启动/UI/卸载 → 严格检查 → 同步版本 → 草稿 |

## 日常流程

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

测试阶段按顺序执行：

1. **并行测试**：Core、UI、StaticImg、WebBackdrop、UIComponent、ML、Shader 七个独立任务。产物检查并行执行；Web 播放器脚本检查随 UI 任务执行。
2. **安装后应用验收**：前置测试及原始报告通过后，编译 `InnoSetup/setup.iss`，只安装一次，从安装目录启动 Core。首次运行必须打开有响应的 UI，随后 UI、Core 正常退出。预构建的 `test/VirtualPaper.UIOperationTest` 宿主再对安装后的程序运行扩展测试集。最后执行真实卸载；安装、启动或 UI 测试失败也会尝试卸载。需要强制恢复或存在文件、进程残留时，验收失败。

有桌面时操作真实安装、卸载向导；无桌面时静默检查文件、版本、快捷方式、注册信息和卸载结果。启动和真实 UI 交互明确标为 `Skipped`，不算通过。锁屏和修改注册表的 UI 测试项仍跳过。测试安装包保留为 `test-installer-package`。

失败会阻止后续测试阶段，但仍收集报告。结果缺失、为空、失败、中止或出现异常时，最终检查失败。七个常规测试项目分别保留各自的 Cobertura 覆盖率报告。

八个项目任务共用 `test/actions/run-project` 和 `test/Run-ProjectTests.ps1`。`test/TestReportSupport.ps1` 负责项目与报告映射，`test/Summarize-TestResults.ps1` 生成汇总。项目任务和汇总使用同一套 TRX 校验逻辑。

`build-output` 保留仓库根目录下的 `src/` 布局、共用测试入口和两个测试宿主的 Release 二进制文件。使用方将其下载到仓库根目录，打包使用同一份构建。UI 宿主保留在安装目录外；应用用例通过 `-CoreExe <安装目录>/VirtualPaper.exe` 运行安装后的 Core、UI、播放器，不重新构建。汇总通过前，安装验收报告与详细 UI 报告的结果必须一致。

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
4. 编译 ISS 并验收实际发布安装包：安装 → 首次启动与正常退出 → 对安装后的程序运行扩展 UI 测试 → 卸载。失败仍执行卸载。使用 `Summarize-TestResults.ps1 -RequireFullAcceptance` 严格检查全部原始报告：失败、缺报告、项目测试跳过、无桌面导致启动/UI 跳过或卸载失败均会阻止后续步骤。发布验收需要可用的 Windows 交互桌面。随后按需生成插件补丁包。
5. 同步版本前再次检查标签和发布冲突。常规发布仅更新 `main`、`dev` 中的以下字段，再通过 `git push --atomic` 原子推送两个分支。任一更新被拒绝，两个分支均不前进：
   - `src/VirtualPaper/VirtualPaper.csproj`：`AssemblyVersion`
   - `InnoSetup/setup.iss`：`MyAppVersion`、`MyAppVersionText`
   - `README.md`：发布版本徽章
6. 根据 PR 描述创建发布草稿。每次发布都包含安装包和完整的 `app_comp_manifest.json`，插件发布还包含补丁包。标签指向同步后的 `main` 提交；插件更新则指向原始合并提交。最终发布仍需手动执行。

阶段依次为 `prepare-metadata → build-release → 并行项目测试 → package-release → sync-versions → draft-release`。插件发布同样必须完整验收，但跳过产品版本同步。安装包验收与可选插件打包共用一个任务。打包或验收失败可单独重试，不重新构建或重跑已成功的项目任务。草稿任务会写入 Actions 摘要。Pre-merge 可接受明确的无桌面跳过；Pre-release 不接受已选测试被跳过。

一份保留仓库根目录布局的构建产物包含应用/插件输出、全部项目测试及两个测试宿主。并行测试和打包下载同一份构建，只有应用输出进入安装包。组件清单仅生成一次，供发布附件和插件补丁共用。锁屏、修改注册表的 UI 用例及真实空闲屏保测试仍按既有策略排除。

组件清单通过 `GITHUB_TOKEN` 认证获取。网络、权限、下载或解析错误都会停止发布。常规发布会为所有随包插件分配新构建号；插件发布仅更新选定插件，其他条目沿用上一个已发布清单。

如果历史发布没有清单，例如未记录构建号的 `v0.5.2.0`，可通过一次完整发布建立基线。所有随包插件使用新构建号，不猜测旧插件版本。历史标题和标签中的构建号仍占用相应号码。没有历史号码时，从当月 `R01` 开始分配。插件补丁必须有有效的基线清单。已有清单损坏时，不会退回到无基线处理路径。

月内序号固定为两位，达到 `R99` 后停止，避免旧客户端通过字符串比较将 `R100` 误判为更早版本。新月份从 `R01` 开始。构建机器的时钟早于历史编号时也会报错。

版本同步会保留打包期间新增到 `dev` 的业务代码和文档，不会完整合并 `main`、强制重置或覆盖分支。远端并发更新、版本字段不符合预期或推送权限不足，都会阻止同步和草稿创建。`.sln` 不包含产品版本，无需修改。`app_comp_manifest.json` 是生成产物。

若同步成功但创建草稿失败，请在原运行记录中选择 **Re-run failed jobs**。因为 `main` 已前进，重新启动整个工作流会被阻止，以免再次递增版本。同一次运行的产物可覆盖之前上传的内容。清单、发布包和发布测试汇总保留 30 天；构建输出和原始测试报告保留 7 天。

已有标签或不兼容的发布会阻止继续。中断的草稿上传仅在标签、标题和目标提交一致时允许续传。远端检查与写入不是同一个原子事务；准备期间不要手动创建同名标签或发布该草稿。不存在自动强制回滚，也没有单独的手动回滚工作流。

## 仓库配置

这些设置必须在 GitHub 上配置，修改本地 YAML 不会改变远端分支规则：

- 为 `main` 要求 PR 和 `pre-merge-check` 状态。若 `dev` 也应仅允许 PR，请配置相同保护。若允许直接推送到 `dev`，推送不会触发这些手动工作流；Pre-merge Check 仍要求一个未合并的 PR。
- 删除 `Check Source Branch`、`ci-check/pre-publish-tests` 等过时的必需检查，新工作流不再提供这些状态。
- 可考虑要求 PR 分支与目标分支保持最新，目标分支变化后重新运行检查。
- `RELEASE_PAT` 必须允许版本同步机器人在仓库规则下更新 `main` 和 `dev`。权限不足时同步失败，工作流不会通过强制推送绕过保护。
- 一次性创建四个固定的 `version:*` 标签。
- 修改本地工作流不会删除远端 `release`、`bugfix` 等旧分支。确认没有未合并工作后再单独清理。

## 本地校验

```powershell
pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1
```

该命令校验工作流依赖、共用测试入口、TRX 和覆盖率检查、重试报告隔离、UI 环境跳过、安装包报告、发布元数据和版本同步契约。它使用隔离 Git 仓库和模拟的 GitHub 响应，不会安装应用、锁定会话、修改注册表或向远端推送。
