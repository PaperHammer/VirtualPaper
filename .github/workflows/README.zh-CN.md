# CI/CD 工作流

[English](README.md) | [简体中文](README.zh-CN.md)

两个主入口均仅通过 `workflow_dispatch` 手动运行；`package-acceptance.yml` 仅接受 `workflow_call` 调用。推送、修改标签及合并不会自动触发。输入 PR 决定固定源码 SHA，Run workflow 选择的分支决定工作流定义；新定义应先进入默认分支。

| 工作流 | 输入 | 打包渠道 |
| --- | --- | --- |
| Pre-merge Check | 未合并的同仓 PR；可选 ML 集成测试 | 始终 EXE + MSIX |
| Pre-release | 已合并的 `dev → main` PR | 始终 EXE；PR 有 `channel:msstore` 时增加 MSIX |

## 构建和验收

共享构建和测试使用 .NET 10。EXE 首次安装及静默升级均检测并补装 x64 Desktop Runtime 和 Web 预览所需的 ASP.NET Core Runtime；MSIX 为每个进程发布已修补的自包含 .NET 10。安装包命名、更新路由和应用标识保持不变。

首次从 .NET 8 迁移的发布必须是完整产品发布：使用 `version:*` 标签，不添加 `update-plugins` 或 `update-plugin-*` 标签。插件补丁不会安装新的系统运行时，也不会替换 Core。

工作流契约检查项目文件激活声明和图标帧。MSIX 打包还检查实际包内的关联参数与图标，安装后的资源管理器激活仍属于桌面验收项。

EXE／MSIX 安装版扩展 UI 报告声称通过时，要求每个已选宿主用例恰好出现一次，包括多语言、壁纸整理、预设、反馈及硬件检查。契约对照必需清单与宿主登记，逐项拒绝缺少或重复的用例；可选真实空闲测试和显式 MSIX 配置阶段仍独立执行。受控反馈夹具不替代真实推理、安装版应用用例或干净机运行时补装。

共用 UI 测试要求包含 `ProjectActivation` 模块，验证真实 Core/UI 的项目打开链路，不修改日常文件关联。

共享真实 UI 测试集新增壁纸整理与效果预设管理用例，详见[测试清单](../../test/README.zh-CN.md#壁纸库)。标准／扩展测试集均包含两项，渠道隔离及冒烟必须通过且无跳过的门禁保持不变。本地源码／编译检查与实际执行 UI 用例分别记录。

两个主入口中相同阶段使用一致的显示名称，包括 `Build Release` 和 `Test Summary`。 Windows 和 Ubuntu 的 run 块均默认使用 PowerShell（`pwsh`），包括打包门禁和汇总。

1. 校验 PR，固定源码 SHA、版本和渠道选择；使用 Visual Studio MSBuild 与解决方案 Any CPU 映射，完整构建一次 Release，包含两个测试宿主。
2. `package-exe`、`package-msix` 并行消费同一构建。EXE 使用 Inno Setup 6.7.1，MSIX 在独立目录发布自包含程序，不能改变 EXE 输出。`packages-ready` 要求选中的包全部生成成功。
3. 所选包全部成功后，八组共享项目测试只运行一轮，内部并行。Artifact Tests 仅消费构建产物，不检出源码；预期文案和映射已嵌入测试程序集。两路渠道验收均等待全部八组成功；失败或跳过时不开始安装。
4. `test-exe-acceptance`、`test-msix-acceptance` 调用同一个 `package-acceptance.yml` 模板，每路启动互无依赖的 Smoke、UI Acceptance 两个 job，共四个 job 并行使用独立的全新 GitHub-hosted Windows VM。每个 job 自行检出同一固定源码、下载并核验对应构建与安装包、安装及卸载，分别保留报告。
5. `Summarize-PipelineTests.ps1` 校验选中渠道的子流程结果及原始冒烟/UI 报告（每渠道两份）、TRX/覆盖率、安装生命周期、真实 UI、源码 SHA 和包哈希。包校验、项目测试和冒烟失败／跳过，身份不符或上传／报告缺失仍阻塞。两个工作流仅允许扩展 UI 运行失败及合法的环境跳过；记录真实步骤结果，继续显示 Failed／Skipped，不伪装为通过。未选择商店渠道显示 `NotSelected`，不能算测试通过。
6. Pre-merge 在 PR head 回写 `pre-merge-check`。Pre-release 验收通过后仅在 main 提交一次产品版本，再将该 main 合回 dev，原子推送两分支。随后 `Draft Release (GitHub)` 和选中的 `Draft Release (Microsoft Store)` 并行执行，互不依赖；一路草稿失败不会阻止另一路执行。最后汇总两者结果。

子流程的两个独立 job 均固定使用 `windows-latest`，各自拒绝非 GitHub-hosted runner，因此每个 Smoke／UI job 使用独立 Windows VM 和桌面；不要求或承诺云端物理宿主不同。同一台自托管 Windows 上的两个 runner 进程不满足隔离要求。冒烟和 UI 各自安装、配置、卸载，互不等待或复用。冒烟及其清理失败仍阻止整体验收，但独立 UI job 可继续完成并上传诊断报告。需要活动且未锁定的 Default 桌面、Explorer、有效显示器、安装/证书信任所需提升权限、运行库和预置模型。桌面不可用必须明确跳过：冒烟仍阻止门禁，扩展 UI 有上传报告时可不阻塞；不能放宽检测或将跳过算作通过。

EXE UI 使用 `Run-InstallerTests.ps1 -ExtendedUIReportPath ...`，自行安装后对已安装 Core 运行扩展 UI，继续完成生命周期并卸载。它不借用冒烟安装，也不以 Release 目录代替安装体验证。已有安装、AppId 和快捷方式保护仍然有效。

MSIX 使用 `Run-MsixTests.ps1 -PackageArtifact ... -Mode Smoke|UI`，直接安装已有签名包，不重新构建。通过 AUMID 激活 Core，检查 Core/UI 包身份与正常退出，最后卸载并删除本轮证书信任。冒烟覆盖首次自动 UI、同包重装、保留/全新配置和内置壁纸；扩展 UI 在独立安装上执行。

MSIX 入口复用已有 x64 VC AppX 框架；仅可丢弃的 GitHub-hosted CI 在缺失时下载并安装微软签名的框架，本地入口要求提前准备。

`package.json` 绑定渠道、源码 SHA、身份/版本和包哈希。跨 job 只传公开证书，打包 job 删除不可导出的私钥。WAP 会分别签名上传包和侧载包：除 `AppxSignature.p7x`、`AppxMetadata/CodeIntegrity.cat` 外，全部应用载荷必须一致；两个包各自保留哈希。正式身份只允许在可丢弃的 GitHub-hosted CI 上验收，并需本轮新建注册的归属证据；本地日常商店安装保护不放宽。

## PR 和版本规则

Pre-merge 支持同仓功能/修复分支进入 `dev`，以及 `dev → main`。后者必须恰好有一个 `version:major`、`version:minor`、`version:build`、`version:revision`。运行前验证合并快照，回写成功前再次核对 PR head/base/版本/商店标签；变化后需手动重跑。

Pre-release 仅接受最新合并的同仓 `dev → main` PR。其 head 必须有对应 PR、版本标签及商店渠道选择的成功检查，且 `main` 仍指向该合并提交。运行期间修改渠道、版本或插件标签会阻止版本写入和草稿。旧格式的成功状态需要重新运行 Pre-merge 更新。

四个版本标签递增产品 `AssemblyVersion` 对应位置并将右侧归零。`update-plugins` 或 `update-plugin-ui/playerweb/scrsaver/ml/shaders` 保留产品版本及既有插件包/tag 兼容格式，跳过产品版本同步；选择商店渠道时仍生成完整 MSIX。

发布元数据从正式发布、草稿和组件清单分配 `YYMMRxx` 构建号；草稿占用的号码仍保留，每月上限 R99。所有 GitHub 发布继续附带独立 `app_comp_manifest.json`。旧发布无清单时可以先完整发布建立基线；仅插件发布要求已有正式清单。

Store 包版本独立于 `AssemblyVersion`：`2611R02` 映射为 `1.2611.2.0`。产品第四段或仅插件更新也会递增 Store 包版本，满足首段非零、末段为零的规则。构建前和创建草稿前均核对商店已发布版本；如商店版本更高则失败，不能静默降级或复用。参阅[微软版本规则](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-package-requirements)。

版本同步仅在 `main` 修改版本字段并创建版本提交，再通过 `--no-ff` 将更新后的 `main` 合入最新 `dev`。`dev` 保留自身开发历史，同时接收同一个 main 版本提交及 main 独有改动。本地两条提交历史准备成功后才原子推送 `main`、`dev`，不强推或绕过保护。文本冲突块采用 main（`-X theirs`）；仍未解决的冲突路径按 main 的文件版本或删除状态处理。dev 中不冲突的改动保留，包括同一文本文件内其他不冲突的块，不将整个 dev 文件树覆盖成 main。Git 操作失败时撤销本轮待提交合并；推送拒绝仍不推进两条远端分支，并阻止草稿。重跑复用 main 版本提交，已完成的合并不重复创建；`RELEASE_PAT` 需要符合仓库规则的写入权限。

## 分开的发布草稿

Pre-release 中全部商店操作由已检查 PR 的精确 `channel:msstore` 标签控制，不读取 Git tag。未带此标签时，跳过 Partner Center 校验、MSIX 打包／安装验收及 Store draft job，无需商店凭据。两路草稿共享验收／版本同步门禁，彼此没有依赖；已选渠道的打包／验收失败仍阻止共同门禁通过。

GitHub 保留原 EXE/插件的发布 tag 和更新资产，不额外创建 MSIX 专属 GitHub Release，保持现有客户端更新选择逻辑。草稿不会自动公开或标为 latest。

有 `channel:msstore` 时，`Invoke-StoreDraft.ps1` 创建/更新 Partner Center MSIX 提交，合入已审查的本地文案、更新说明和桌面截图，保留商店名称、未管理的条目数据、价格、声明及其他架构，替换原 x64 包，将已验证的 Store 包和引用图片一并上传。保留 `targetPublishMode=Manual`、`status=PendingCommit`，不会调用提交、请求认证或上架接口。上传成功不等于认证通过。

重试只复用同仓库、同 workflow run、同源码 SHA、包哈希及材料哈希所拥有的未提交草稿。其他运行、人工草稿或已提交草稿均拒绝修改，更不会删除。如果创建成功但未写入归属标记，需要显式处理该待提交草稿后再重试。API 创建的提交应继续通过 API 管理，微软文档提示不要转到 Partner Center 编辑同一提交。参阅[商店 API 说明](https://learn.microsoft.com/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services)。

统一维护标准见 [resources/store](../../resources/store/README.zh-CN.md)：当前文案放在 `listings/<locale>.json`，版本说明放在 `releases/<计算后的产品版本>.json`，图片及共享／来源登记放在 `screenshots/`。准备阶段提前检查，草稿 job 从同一已提交快照导出并保留可阅读的 `store-materials` 产物；首次商店提交自动留空更新说明。也可运行 `Export-StoreMaterials.ps1 -Version <产品版本> -InitialRelease` 离线交接，不写远端。修改文案／图片需要新的已审查源码快照，旧 job rerun 仍使用原材料。

## 补充检查与版本标签

Pre-merge 默认排除分类为 `Integration` 的 ML 测试，勾选 **Also run real model inference tests** 可纳入；Pre-release 始终包含。常规 ML 测试需要预先准备模型，不下载模型，文件缺失时记录跳过；选中的测试存在跳过会阻塞工作流。完整 UI 的 ModelInference 用例缺少模型时直接失败。本地运行常规模型集成测试：

```powershell
dotnet test src/VirtualPaper.ML.Test/VirtualPaper.ML.Test.csproj --configuration Release --filter "TestCategory=Integration" --settings src/test.runsettings
```

两个安装包渠道的扩展验收还强制要求四项 `HardwareAcceleration` UI 用例齐全：延迟到重启生效的启动策略、软件模式 Web/绘图链路，以及实际 CPU/请求 GPU 的超分与风格迁移后高清处理。模型必须随包提供，包括 AdaIN 外部权重。报告区分 GPU 成功执行和 CPU 回退；不支持 GPU 的硬件可验证正常回退，但不能记为 GPU 执行成功。详见[硬件加速 UI 验收说明](../../test/README.zh-CN.md#硬件加速)。

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

产品版本同步仅修改 `src/VirtualPaper/VirtualPaper.csproj` 中的 `AssemblyVersion`、`packaging/inno/setup.iss` 中的 `MyAppVersion`/`MyAppVersionText`，以及 `README.md` 的版本徽章。`.sln` 没有产品版本，`app_comp_manifest.json` 是生成产物。main 的版本提交合回 dev，不再单独生成 dev 版本提交；下一次 dev 合入 main 时沿用这段共同的发布历史。

组件清单仅生成一次，供发布附件和插件补丁复用，并通过 `GITHUB_TOKEN` 获取；网络、权限、下载或解析错误会停止发布。历史发布缺少清单时，可通过完整发布建立基线；插件补丁必须有有效的已发布基线，已有清单损坏时不得退回无基线处理。完整发布更新所有随包插件构建号，插件发布仅更新选定项。月内序号达到 R99 后停止，新月份重新开始，构建机器时钟落后于历史编号时也会报错。

若版本同步或一个草稿已成功，而后续步骤失败，应在原运行选择 **Re-run failed jobs**。匹配的 GitHub/Store 草稿可续传；不自动回滚外部状态。版本同步已推进 `main` 后，不要重新启动整个工作流。

## 仓库配置

带 `channel:msstore` 运行 Pre-release 前补齐以下配置。缺失时准备阶段失败，不构建、不写版本、不创建草稿。Pre-merge 无需这些配置，使用每轮唯一的测试身份。

四项公开标识填写到 [packaging/msix/StoreIdentity.json](../../packaging/msix/StoreIdentity.json)，本地商店打包、CI 打包和提交 API 共用该默认配置。原有 `MSSTORE_*` 环境变量仍可覆盖，身份验证凭据不放入 JSON。

| 名称 | 类型 | 内容 |
| --- | --- | --- |
| `StoreId` / `MSSTORE_APPLICATION_ID` | JSON／可选 Repository variable 覆盖 | 产品 Store ID |
| `IdentityName` / `MSSTORE_IDENTITY_NAME` | JSON／可选 Repository variable 覆盖 | Package/Identity/Name |
| `Publisher` / `MSSTORE_PUBLISHER` | JSON／可选 Repository variable 覆盖 | Package/Identity/Publisher |
| `PublisherDisplayName` / `MSSTORE_PUBLISHER_DISPLAY_NAME` | JSON／可选 Repository variable 覆盖 | PublisherDisplayName |
| `MSSTORE_TENANT_ID` | Repository variable | Entra 租户 GUID |
| `MSSTORE_CLIENT_ID` | Repository variable | 关联应用 GUID |
| `MSSTORE_CLIENT_SECRET` | Repository secret | 具有 Partner Center Manager 权限的应用密钥 |

- 为 `main` 要求 PR 和 `pre-merge-check` 状态。若 `dev` 也应仅允许 PR，请配置相同保护。若允许直接推送到 `dev`，推送不会触发这些手动工作流；Pre-merge Check 仍要求一个未合并的 PR。
- 删除 `Check Source Branch`、`ci-check/pre-publish-tests` 等过时的必需检查，新工作流不再提供这些状态。
- 可考虑要求 PR 分支与目标分支保持最新，目标分支变化后重新运行检查。
- `RELEASE_PAT` 必须允许版本同步机器人在仓库规则下更新 `main` 和 `dev`。`dev` 需要允许 main 合回产生的合并提交；若启用线性历史限制，应为机器人设置适当豁免或调整该规则。权限不足时同步失败，工作流不会通过强制推送绕过保护。
- 一次性创建四个固定的 `version:*` 标签。
- 修改本地工作流不会删除远端 `release`、`bugfix` 等旧分支。确认没有未合并工作后再单独清理。

保留四个 `version:*` 标签，并添加 `channel:msstore`。分支规则要求 `pre-merge-check`，旧检查名称另行移除。修改本地文件不会远端创建标签或修改保护；工作流运行期间不要手动创建同名 tag 或发布草稿。

## 报告和本地验证

工作流契约还会运行 `test/Test-LegalDocuments.ps1`，检查全部语种离线说明、章节标识、链接、界面文案与声明原文。产物测试检查随包文件字节，MSIX 校验要求完整法律文档。文档公开后，另行在 Partner Center 配置公开的隐私政策 URL。

跨版本升级使用独立本地入口 `test/Run-UpgradeTests.ps1`，输入两个源码／安装包快照及各自匹配的测试工具，不替代强制同包覆盖安装，也不触发工作流。区域格式／字体用例纳入原有真实 UI 清单；准备方式与截图检查见[测试指南](../../test/README.zh-CN.md)。

原始报告使用 `test-results-*`，汇总使用 `test-summary-*`/`release-test-summary`。JSON/Markdown 记录渠道结果、源码 SHA，包清单记录版本和哈希。`store-draft.json` 记录提交 ID、上传包／安装包／材料哈希、产品版本、语种清单与 `Committed=false`，不包含 token 或 SAS URL。包/草稿/汇总保留 30 天，构建/原始报告保留 7 天；上传支持覆盖重试，下载保留摘要校验。

运行 `pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1` 验证 DAG（打包 → 全部共享项目测试 → 渠道子流程）、固定 hosted VM 隔离、冒烟/UI 顺序、严格报告失败/跳过/缺失、包载荷、版本同步、插件包，以及商店 HTTP 模拟和重试契约。它不安装应用，也不连接 Partner Center/GitHub。完整本地入口仍为 EXE 的 `test/Run-Tests.ps1` 和 MSIX 的 `test/Run-MsixTests.ps1`，详见[测试说明](../../test/README.zh-CN.md)。

该入口同时运行语言生成契约：清单／key／占位符一致性，UI/Core/Common/安装器/MSIX 输出，临时第三语种，过期输出检测和源目录保护。两个渠道共用[语言清单与文案](../../localization/README.zh-CN.md)。包体检查核验实际应用翻译及 MSIX 语种声明，不能用框架卫星资源目录代替产品翻译覆盖。

资源载荷契约还拒绝不完整／过期的 UI 字典及缺失的 Core/Common 资源；产物 runner 在运行 TRX 测试前检查真实 Release 资源，已安装 EXE 与最终 MSIX 使用同一严格校验。真实 UI 用例遍历全部语种，并增加德／法／俄语布局样本。法语安装为本地／手动可选入口，不改变 CI 必需的默认中英文冒烟顺序。
