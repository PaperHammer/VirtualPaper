# GitHub Actions CI/CD 配置说明

## 概述

为VirtualPaper项目配置了完整的CI/CD流程，包含两个工作流文件：

- **dev-ci.yml**: 当代码推送到dev分支时自动运行4个测试项目，并创建状态检查
- **branch-protection.yml**: 确保只有dev分支可以合并到main分支

## 工作流程

### 1. Dev分支持续集成 (dev-ci.yml)

**触发条件：**
- 推送到dev分支
- 创建针对dev分支的PR

**执行步骤：**
1. 设置.NET环境（8.0.x）
2. 恢复项目依赖
3. 构建解决方案
4. 按顺序运行4个测试项目：
   - VirtualPaper.Core.Test
   - VirtualPaper.UI.Test
   - VirtualPaper.ML.Test
   - VirtualPaper.Shader.Test
5. 发布测试结果报告
6. 创建commit状态检查（context: `ci/dev-tests`）

### 2. 分支保护检查 (branch-protection.yml)

**触发条件：**
- 创建针对main分支的PR
- 更新针对main分支的PR

**检查逻辑：**
1. 验证源分支是否为dev
2. 如果不是dev分支，创建失败状态并阻止合并
3. 在PR中添加友好的提示评论

## 分支保护规则设置

为确保只有测试通过的代码才能合并到main分支，需要在GitHub仓库中设置分支保护规则：

### 设置步骤：

1. 进入GitHub仓库 → Settings → Branches
2. 为main分支添加或编辑保护规则：

```
分支名称模式: main

☑️ Require a pull request before merging
    ☑️ Require approvals: 1
    ☑️ Dismiss stale reviews when new commits are pushed

☑️ Require status checks to pass before merging
    ☑️ Require branches to be up to date before merging
    添加必需的状态检查：
    - ci/dev-tests
    - branch-protection/source-validation

☑️ Include administrators
```

## 工作原理

1. **开发阶段**: 
   - Feature分支合并到dev时，自动运行测试
   - 只有dev分支可以创建到main的PR
2. **发布阶段**: 
   - 创建dev→main的PR时，检查两个状态：
     - `ci/dev-tests`: dev分支最新commit的测试结果
     - `branch-protection/source-validation`: 源分支验证结果
3. **合并控制**: 
   - 只有两个状态都为success时，PR才能被合并


## 使用流程

### 日常开发：
1. Feature分支 → dev分支（触发测试）
2. 查看dev分支commit旁的测试状态

### 发布流程：
1. 创建dev → main的PR
2. 系统自动检查：
   - 源分支是否为dev（`branch-protection/source-validation`）
   - dev分支最新commit的测试结果（`ci/dev-tests`）
3. 两个检查都通过时才能合并

## 错误处理

### 如果从非dev分支创建PR到main：
- ❌ 工作流会失败并创建失败状态
- 📝 PR中会显示友好的错误说明
- 🔒 PR会被阻止合并

### 如果dev分支测试失败：
- ❌ `ci/dev-tests`状态为failure
- 🔒 PR会被阻止合并
- 🛠️ 需要修复测试问题后重新推送到dev

## 注意事项

- 确保dev分支有最新的测试结果
- 分支保护规则中需要添加两个状态检查
- 只有dev分支可以合并到main分支