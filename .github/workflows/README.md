# GitHub Actions CI/CD 配置说明

## 概述

已为您的VirtualPaper项目配置了完整的CI/CD流程：

1. **dev-ci.yml**: 当代码推送到dev分支时自动运行4个测试项目
2. **pr-check.yml**: 在创建从dev到main的PR时检查dev分支的测试状态

## 工作流程

### 1. Dev分支持续集成 (dev-ci.yml)

**触发条件：**
- 推送到dev分支
- 创建针对dev分支的PR

**执行步骤：**
1. 设置.NET环境（支持6.0.x, 7.0.x, 8.0.x）
2. 恢复项目依赖
3. 构建解决方案
4. 按顺序运行4个测试项目：
   - VirtualPaper.Core.Test
   - VirtualPaper.UI.Test
   - VirtualPaper.ML.Test
   - VirtualPaper.Shader.Test
5. 发布测试结果报告
6. 创建commit状态检查

### 2. PR检查工作流 (pr-check.yml)

**触发条件：**
- 创建从dev到main的PR
- PR更新（synchronize, reopened）

**检查逻辑：**
1. 验证源分支是dev
2. 获取dev分支最新commit的测试状态
3. 根据测试结果决定是否允许合并
4. 在PR中添加状态评论

## 分支保护规则设置（重要）

为了确保只有测试通过的代码才能合并到main分支，您需要在GitHub仓库中设置分支保护规则：

### 设置步骤：

1. 进入您的GitHub仓库
2. 点击 Settings → Branches
3. 点击 "Add rule" 或编辑现有的main分支规则
4. 配置以下设置：

```
分支名称模式: main

☑️ Restrict pushes that create files larger than 100 MB
☑️ Require a pull request before merging
    ☑️ Require approvals: 1
    ☑️ Dismiss stale reviews when new commits are pushed
☑️ Require status checks to pass before merging
    ☑️ Require branches to be up to date before merging
    在搜索框中添加以下状态检查：
    - check-dev-tests
    - ci/dev-tests
☑️ Require conversation resolution before merging
☑️ Include administrators
```

### 状态检查说明：

- **ci/dev-tests**: dev-ci.yml工作流创建的状态检查
- **check-dev-tests**: pr-check.yml工作流创建的状态检查

## 使用流程

### 日常开发：
1. 在feature分支开发
2. 创建PR合并到dev分支
3. 合并后自动触发测试

### 发布流程：
1. 确保dev分支所有测试通过
2. 创建从dev到main的PR
3. 系统自动检查dev分支测试状态
4. 只有测试通过才能完成合并

## 测试结果查看

- **测试报告**: 在Actions页面查看详细的测试报告
- **PR评论**: 自动在PR中添加测试状态评论
- **状态徽章**: commit旁边显示测试状态

## 故障排除

### 如果测试失败：
1. 查看Actions页面的详细日志
2. 修复测试问题
3. 推送修复代码到dev分支
4. 等待新的测试结果

### 如果PR被阻止：
1. 确保dev分支最新提交测试通过
2. 如果测试正在运行，等待完成
3. 如果测试失败，修复后重新推送

## 自定义配置

如果需要修改配置，可以编辑以下文件：
- `.github/workflows/dev-ci.yml`: 修改测试执行逻辑
- `.github/workflows/pr-check.yml`: 修改PR检查逻辑

## 注意事项

1. 确保所有测试项目都能正常运行
2. 测试失败时，`continue-on-error: false` 会阻止后续步骤
3. 需要适当的GitHub权限来设置分支保护规则
4. 工作流需要 `GITHUB_TOKEN` 权限（默认提供）