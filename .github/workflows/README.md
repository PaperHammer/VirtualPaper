# CI/CD 工作流说明

## 文件总览

| 文件                                | 触发时机                              | 职责                                 |
| ----------------------------------- | ------------------------------------- | ------------------------------------ |
| `pre-publish-branch-ci-check.yml` | push / PR → dev · release · bugfix | 构建 + 单元测试                      |
| `branch-protection.yml`           | PR → main                            | 校验源分支合法性                     |
| `auto-version-release.yml`        | PR 合并到 main                        | 版本递增 · 打包 · 冒烟测试 · 发布 |

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

1. 构建整个解决方案（Release 配置）
2. 并行运行 4 项单元测试：Core · UI · ML · Shader
3. 汇总测试结果，写入 commit status `ci-check/pre-publish-tests`

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
  │  静默安装 → 启动主程序 → 等待 UI 被拉起（最多 10s）
  │  若未自动拉起：兜底手动启动 UI
  │  再次尝试启动 UI → 断言只有 1 个 UI 进程（单例验证）
  │  验证 UI 无法在无主进程时独立运行（MessageBox 守卫）
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
