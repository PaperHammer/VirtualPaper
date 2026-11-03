# CI/CD 工作流

只保留两个工作流，均仅使用 `workflow_dispatch`。提交代码、创建或更新 PR、增删标签、合并 PR 都不会自动创建运行。

| 文件 | 手动输入 | 职责 |
| --- | --- | --- |
| `pre-merge-check.yml` | 未合并的 PR 编号；是否包含模型集成测试 | 检查来源与版本标签 → 构建 → 安装生命周期验收 → 并行测试 → 真实 UI 验收 → 回写 PR 状态 |
| `pre-release.yml` | 已合并的 `dev → main` PR 编号 | 验证检查结果 → 计算版本 → 构建打包 → 同步版本字段 → 生成 Draft Release |

## 日常操作

1. 功能／修复分支向 `dev` 创建 PR。
2. Actions → **Pre-merge Check** → **Run workflow**，填写 PR 编号。
3. 检查成功后合入 `dev`。
4. 创建 `dev → main` PR，选择一个版本标签，再手动运行 **Pre-merge Check**。
5. 检查成功后合入 `main`。
6. Actions → **Pre-release** → **Run workflow**，填写刚合并的 PR 编号。
7. 下载 Draft Release 附件验证，再手动正式发布。

`Run workflow` 表单中的分支用于选择工作流定义；测试和发布内容由填写的 PR 编号决定，不会把表单分支的 HEAD 误当作被测提交。建议从默认分支运行。
不再使用 `ci-check-approval`、`publish-approval` Environment，也无需再批准 Deployment。

工作流文件必须先进入默认分支，GitHub 才会显示手动入口。首次迁移时需由管理员安排旧必需检查与新检查的切换，避免等待已删除的检查。
参见 [GitHub 手动运行工作流](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow)。

## Pre-merge Check

仅支持同仓库 PR：

- 合入 `main`：只接受 `dev`，四个版本标签必须恰好选择一个。
- 合入 `dev`：接受 `main`、`dev` 以外的功能／修复分支，不要求版本标签。
- 其他目标分支和 fork PR 拒绝执行。

每次手动运行都重新读取来源、标签、PR head 和目标分支，验证 GitHub 的合并快照对应双方当前提交。所有构建和测试固定使用这份合并快照。

测试阶段严格串行：

1. **安装生命周期验收**：使用实际安装包安装 → 从安装目录启动 Core，由首次运行自动打开 UI → 正常关闭 UI 和退出 Core → 运行实际卸载程序。恢复时强杀、文件或进程残留均判失败。 有桌面时操作真实安装／卸载向导；无桌面时静默验收文件、版本、快捷方式、注册信息和卸载结果，窗口打开明确跳过。
2. **并行测试**：Core、UI、StaticImg、WebBackdrop、UIComponent、ML、Shader 七个独立 job；构建产物检查另行并行，Web 播放器脚本检查随 UI job 执行。
3. **真实 UI 交互**：前两阶段全部通过后，运行 `test/VirtualPaper.UIOperationTest`。会启动真实前后台进程；无可用交互桌面时明确标为 `Skipped`，不冒充 UI 验收通过。锁屏、注册表修改测试保持跳过。

任一阶段失败，后续测试阶段不启动。报告仍会收集；缺失、零执行、失败、中止或异常的报告会使最终检查失败。普通单元测试继续保留七份 Cobertura 覆盖率。

八个项目 job 共用 `test/actions/run-project` 和 `test/Run-ProjectTests.ps1`，项目与报告映射由 `test/TestReportSupport.ps1` 维护，继续独立并行。汇总由 `test/Summarize-TestResults.ps1` 完成，各项目与最终汇总复用同一份 TRX 校验逻辑。

`build-output` 保留仓库根目录下的 `src/`、共享测试入口及 WinUI 宿主的 Release 产物，所有消费端下载到仓库根目录。真实 UI 阶段使用 `-NoBuild -HostExe` 运行这批宿主与应用二进制，不再重新构建解决方案。

原始报告保持 `test-results-*` 前缀，汇总包改为 `test-summary-*`；下载原始报告时不会再次下载汇总包。上传明确设置 `overwrite: true`，支持在同一次运行中重跑已上传过报告的 job；汇总只读取各项目的指定报告路径，不递归吞入其他包。

默认排除 ML 的 `Integration` 分类。勾选 **Also run real model inference tests** 可合并执行；需要对应模型文件，模型缺失时由现有测试明确跳过，不自动下载。
也可本地单独运行：

```powershell
dotnet test src/VirtualPaper.ML.Test/VirtualPaper.ML.Test.csproj --configuration Release --filter "TestCategory=Integration" --settings src/test.runsettings
```

输入已关闭／已合并 PR 或 fork PR 时直接拒绝，不修改此前检查状态。合法开放 PR 开始检查时，在 PR head SHA 上写入 `pre-merge-check: pending`；最终重新核对 PR 提交、目标分支和版本标签，再写入成功／失败。
该结果属于 PR 提交，不属于手动表单所选分支。[GitHub commit status API](https://docs.github.com/en/rest/commits/statuses) 支持以 SHA 和 context 记录该检查。

更新 PR 提交后必须手动重跑，原提交的通过结果不能满足新提交的必需检查。增删标签不自动创建运行，也不会自动撤销同一提交已有的通过结果，因此修改标签后也必须手动重跑；发布入口会再次校验当前标签。
测试进行期间若 PR 内容、目标提交或版本选择发生变化，本轮不能通过。手动取消后的 pending 结果同样不能用于合并，需要重新运行。

## 版本标签

| 标签 | 增长位置，右侧归零 | 从 `0.5.2.0` 得到 |
| --- | --- | --- |
| `version:major` | 第一位 | `1.0.0.0` |
| `version:minor` | 第二位 | `0.6.0.0` |
| `version:build` | 第三位 | `0.5.3.0` |
| `version:revision` | 第四位 | `0.5.2.1` |

这些标签用于产品四段版本。无标签或多个标签均拒绝发布，不再按来源分支计算增长位。
独立插件更新继续使用 `update-plugins` 或 `update-plugin-ui/playerweb/scrsaver/ml/shaders`，沿用现有插件 build number 与客户端兼容的 tag 格式。
带插件更新标签时产品版本保持不变、不提交产品版本同步；`dev → main` PR 仍需通过四选一版本标签校验。

## Pre-release

只接受已合并的同仓库 `dev → main` PR，要求其 head 已有对应本 PR 和当前版本标签的成功 `pre-merge-check` 状态，且 `main` 仍指向该 PR 的合并提交。仅关闭未合并、旧 PR、测试未通过或版本标签不合法均阻断。

1. 从固定合并提交的 `AssemblyVersion` 和所选标签计算目标版本；插件更新保持产品版本不变。读取已发布 Release 的组件清单，结合历史 Release 标题／插件 Tag 中的构建号计算新的 `YYMMRxx`；Draft 中已使用的编号也保留。构建前检查目标 Tag 和 Release 是否冲突。
2. 编译前临时修改 `VirtualPaper.csproj` 的版本，制作安装包前临时修改 `InnoSetup/setup.iss` 的版本。产物使用目标版本。
3. 校验构建产物，制作安装包；需要插件包时生成现有插件补丁包和 manifest。发布流程不执行安装／打开／卸载，安装生命周期验收保留在 Pre-merge Check。
4. 版本同步前再次检查目标 Tag 和 Release。常规发布只更新 `main`、`dev` 的以下字段，使用 `git push --atomic` 同时提交两个分支；任一分支推送被拒绝则两者都不更新：
   - `src/VirtualPaper/VirtualPaper.csproj`：`AssemblyVersion`
   - `InnoSetup/setup.iss`：`MyAppVersion`、`MyAppVersionText`
   - `README.md`：版本徽章
5. 全部成功后创建 Draft Release，正文沿用 PR 描述，所有发布均附带安装包和完整 `app_comp_manifest.json`；插件发布额外附带插件补丁。Tag 指向实际同步后的 `main` 提交；插件更新指向原合并提交。正式发布仍需人工完成。

工作流保留五个 job：`prepare-metadata → build-release → package-release → sync-versions → draft-release`。插件发布跳过版本同步，打包成功后直接进入 Draft；安装包和可选插件包共用同一个打包 job。构建仍独立，打包失败可单独重跑，无需重新编译。汇总写入 Draft job 的 Actions Summary，不再单独启动 job。

构建 artifact 仅包含主程序 Release 输出目录（含所有插件），打包 job 下载到相同目录；不再上传整个 `src/`。组件清单生成一次，发布附件与插件补丁均复用同一份文件。

组件清单通过 `GITHUB_TOKEN` 认证读取，网络、权限、下载或解析失败均停止发布。普通发布为所有随安装包交付的插件更新构建号；插件发布只更新选中插件，其余沿用上个正式发布的清单。历史 Release 缺少清单时，若标题／插件 Tag 留有构建号，可先做一次完整发布建立基线；不能直接发插件补丁。若构建号也无法确认，需补齐历史原始清单，避免猜测版本。

构建号保持两位月内序号，达到 `R99` 后本月停止继续分配，避免旧客户端按字符串比较时把 `R100` 误判为更旧；跨月重新从 `R01` 开始。构建机时间落后于历史编号也会报错。

版本同步保留 `dev` 在打包期间新增的业务代码和文档；不再全量合并 `main`，也不强制重置、覆盖分支。若远端出现并发提交、版本字段异常或推送权限不足，停止同步和 Draft 创建。
`.sln` 中没有产品版本，不需要修改。`app_comp_manifest.json` 属于生成产物。

若同步成功但后续 Draft 创建失败，使用原运行的 **Re-run failed jobs** 继续；整个流程重新运行会被“main 已推进”的检查阻断，避免二次递增。上传 artifact 支持覆盖同次运行的旧产物；组件清单和发布包保留 30 天，完整编译产物保留 1 天。已有 Tag 或不匹配的 Release 会阻断；上传中断留下的 Draft 仅在 Tag、标题和目标提交均匹配时允许续传。检查与远端写入之间仍不是原子事务，期间请勿手动创建相同 Tag 或发布该 Draft。
不再提供自动强制回滚或独立手动回滚 workflow。

## 仓库配置

以下是需要在 GitHub 设置中完成的配置，本地 YAML 修改不会自动替你修改远端规则：

- `main`、`dev` 启用 **Require a pull request before merging**，必需状态统一选择 `pre-merge-check`。
- 移除旧必需检查 `Check Source Branch`、`ci-check/pre-publish-tests`，否则新流程不会提供它们，PR 会一直等待。
- 建议要求分支与目标分支保持最新，目标分支变化后重新运行检查。
- `RELEASE_PAT` 用于机器人提交版本字段，需要能按仓库规则更新 `main`、`dev`。没有权限时同步会失败，不会强推绕过保护。
- 四个固定 `version:*` 标签需在仓库创建一次。
- `release`、`bugfix` 等旧远端分支不会被此次本地修改删除，可确认无未合并内容后另行清理。

## 本地验证

```powershell
pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1
```

包含工作流依赖、共享测试入口、TRX／覆盖率校验、重跑报告隔离、UI 环境跳过、安装生命周期验收报告、发布元数据和版本同步契约检查；使用隔离 Git 仓库及模拟 GitHub 响应，不安装应用、不锁屏、不修改注册表、不推送远端仓库。
