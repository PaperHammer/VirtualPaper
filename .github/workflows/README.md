# CI/CD 工作流说明

## 文件总览

| 文件                                | 触发时机                              | 职责                                 |
| ----------------------------------- | ------------------------------------- | ------------------------------------ |
| `pre-publish-branch-ci-check.yml` | push / PR → dev · release · bugfix | 安装冒烟 → 并行单元测试 → 真实 UI 测试 |
| `branch-protection.yml`           | PR → main                            | 校验源分支合法性                     |
| `auto-version-release.yml`        | PR 合并到 main                        | 版本递增 · 打包 · 冒烟测试 · 发布 |
| `manual-ml-integration.yml`       | 手动触发                              | ML 模型集成测试                    |

---

## 整体流程

```
 feature/* ──►  dev  ──┐
                        │  CI 通过后创建 PR → main
 release   ──────────── ┤
                        │
 bugfix    ──────────── ┘
                        │
                        ▼
                       main  ──►  自动构建打包  ──►  草稿 Release
                                                         │
                                                    人工验证后发布
```

---

## 工作流一：Pre-publish CI

> **文件：** `pre-publish-branch-ci-check.yml`
> **触发：** push 或 PR 到 `dev` / `release` / `bugfix`

对预发布分支进行持续集成，确保代码在合并到 main 之前质量达标。

**执行内容：**

构建整个解决方案（Release 配置）作为准备步骤，之后三个测试阶段严格串行：

1. **Smoke：安装 → 拉起 → 卸载。** 编译真实 Inno Setup 安装包，静默安装到临时目录，启动 Core 并通过正式 `ShowUI` RPC 拉起稳定的 UI 窗口，最后静默卸载并检查进程与文件残留。
2. **并行单元测试。** 保留 7 项常规测试独立并行：Core · UI · StaticImg · WebBackdrop · UIComponent · ML · Shader；产物完整性校验另行并行，Web 播放器脚本检查随 UI 单元测试运行。
3. **真实 UI 测试。** 前两阶段全部成功后，运行 `test/VirtualPaper.UIOperationTest` 中的真实 WinUI 交互测试。

任一阶段失败，后续测试阶段不启动。smoke 的卸载属于必要清理，即使安装／拉起失败也会尝试执行；三个步骤的结果都写入 `test-results-smoke`。不再在 smoke 中运行下载、单例、独立 UI 守卫、播放器或屏保测试，也不直接启动 UI.exe 来掩盖前后台链路失败。

七项常规测试使用共享 `src/test.runsettings` 生成 Cobertura 覆盖率数据。汇总始终检查安装冒烟、常规 TRX 和 UI 报告，写入 commit status `ci-check/pre-publish-tests`；失败后的报告收集不属于后续测试阶段。

Core 测试中的 `Workflow` 分类覆盖配置落盘、壁纸入库应用删除及近期功能的业务集成链路。
WinUI 操作测试包含特效页面组件用例，以及真实 Core + UI 的导航动画设置用例：
经跨进程 UI Automation 修改开关，验证后台 RPC/JSON 保存、UI 进程重开回显及正常退出。
各进程共用隔离的测试会话、配置与通信名称；测试日志随报告保留。
以 JSON 上传 `test-results-ui-operation`，不计入七项常规测试的覆盖率。
它需要可用的 Windows 图形会话；脚本在启动测试前检查会话、输入桌面、显示区域及 Explorer 桌面。
不满足时生成明确的 `Skipped` 报告，CI 允许继续，最终状态注明 `Regular tests passed; WinUI skipped [SKIPPED]`。
跳过不计为 UI 验收通过。报告缺失、不完整及测试启动后的失败、超时仍阻断最终检查。

常规 ML job 排除需要真实模型推理的 `Integration` 分类。

该 status 是合并到 main 的**必需检查项**之一。

---

## 工作流二：分支保护

> **文件：** `branch-protection.yml`
> **触发：** 创建 / 更新 PR → `main`

只允许 `dev`、`release`、`bugfix` 三个分支向 main 发起 PR，其他分支一律拦截。

| 源分支      | 版本变化    | 说明               |
| ----------- | ----------- | ------------------ |
| `release` | Minor +1    | 0.4.x.x → 0.5.0.0 |
| `dev`     | Build +1    | 0.5.2.x → 0.5.3.0 |
| `bugfix`  | Revision +1 | 0.5.3.0 → 0.5.3.1 |

---

## 工作流三：自动发布

> **文件：** `auto-version-release.yml`
> **触发：** PR 成功合并到 `main`（标题不含 `[skip ci]`）

### 更新类型判定

工作流根据 PR 标签自动区分两种更新类型：

| 类型                     | 判定条件                    | 版本号变化                                                   |
| ------------------------ | --------------------------- | ------------------------------------------------------------ |
| **常规安装式更新** | 无 `update-plugin-*` 标签 | 按分支递增（feature→minor, daily→build, bugfix→revision） |
| **重启式更新**     | 含 `update-plugin-*` 标签 | 版本号不变，打包插件                                         |

### 执行流程

```
prepare
  │  计算新版本号，记录各分支 SHA，捕获 PR 标题和正文
  ▼
build
  │  MSBuild Release 构建，产物上传为 Artifact
  ▼
package
  │  InnoSetup 编译安装包，上传为 Artifact（保留 30 天）
  ▼
smoke_test
  │  与预发布 CI 共用 test/Run-SmokeTests.ps1
  │  静默安装 → Core 经正式 RPC 拉起稳定的 UI → 静默卸载／残留检查
  │  启动失败也尝试卸载；三个步骤全部成功才允许后续发布
  │  输出结构化 JSON、安装／卸载日志和隔离应用数据
  ▼
 ┌─────────────────────────────────────────────────────────────┐
 │  以下阶段根据更新类型和测试结果有条件执行（见下表）            │
 └─────────────────────────────────────────────────────────────┘
  ▼
package_restart_update （仅重启式更新）
  │  打包插件 zip + manifest，上传为 Artifact
  ▼
bump （仅常规安装式更新）
  │  更新 csproj / InnoSetup / README badge 版本号并提交到 main
  ▼
sync （仅常规安装式更新）
  │  将版本文件同步到 release / dev / bugfix 分支
  ▼
summary
  │  打印构建摘要
  ▼
release（草稿）
     创建 Draft GitHub Release
     - Tag 和标题：v{版本号}
     - 正文：本次 PR 的描述
     - 附件：安装包 exe（+ 插件包，如有）
     - 状态：草稿，不公开，等待人工验证后发布
```

### 各阶段执行条件

| 阶段                       | 常规安装式更新           | 重启式更新                |
| -------------------------- | ------------------------ | ------------------------- |
| `prepare`                | ✅ 始终执行              | ✅ 始终执行               |
| `build`                  | ✅ 始终执行              | ✅ 始终执行               |
| `package`                | ✅ 始终执行              | ✅ 始终执行               |
| `smoke_test`             | ✅ 始终执行              | ✅ 始终执行               |
| `package_restart_update` | ❌ 始终跳过              | ✅ smoke_test 通过时执行  |
| `bump`                   | ✅ smoke_test 通过时执行 | ❌ 始终跳过               |
| `sync`                   | ✅ bump 成功时执行       | ❌ 始终跳过               |
| `summary`                | ✅ 全部成功时执行        | ✅ 全部成功时执行         |
| `release`                | ✅ summary 成功时执行    | ✅ summary 成功时执行     |
| `rollback`               | ✅ sync 阶段失败时触发   | ❌ 始终跳过（无版本同步） |

### 失败处理

| 失败阶段                   | 常规安装式更新                                                  | 重启式更新                       |
| -------------------------- | --------------------------------------------------------------- | -------------------------------- |
| `build` / `package`    | 流程终止，无后续操作                                            | 流程终止，无后续操作             |
| `smoke_test`             | 流程终止，不触发版本同步和 draft                                | 流程终止，不触发插件打包和 draft |
| `bump`                   | 流程终止，不触发 sync                                           | —                               |
| `sync`                   | **触发 rollback**，回滚 main + 三个预发布分支到 CI 前 SHA | —                               |
| `package_restart_update` | —                                                              | 流程终止，不触发 draft           |

---

## 工作流四：手动 ML 集成测试

> **文件：** `manual-ml-integration.yml`
> **触发：** GitHub Actions 页面手动运行

使用仓库内模型运行 `TestCategory=Integration` 测试。测试结果、TRX 与覆盖率文件保留 7 天，不阻塞常规分支 CI。

---

## 发版操作步骤

```
1. 在 dev / release / bugfix 分支完成开发
2. 确认 CI 全绿（ci-check/pre-publish-tests）
3. 创建 PR → main，等待审核通过并合并
4. CI 自动完成：构建 → 打包 → 冒烟测试 → 提交版本号 → 创建草稿 Release
5. 在 GitHub Releases 页面下载草稿附件，本地安装验证
6. 确认无误后点击 Edit → Publish release 正式发布
```

---

## 分支保护规则（需手动配置）

> Settings → Branches → Add branch protection rule → `main`

```
☑ Require a pull request before merging
☑ Require status checks to pass before merging
    必需检查：
    - ci-check/pre-publish-tests
    - Check Source Branch
☑ Include administrators
```
