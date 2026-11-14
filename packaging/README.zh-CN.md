# 打包说明

[English](README.md) | [简体中文](README.zh-CN.md)

从仓库根目录执行命令，先按[开发指南](../doc/development-guide.md)完成完整 Release 解决方案构建。共用的构建、版本与发布自动化仍位于 [`.github/scripts`](../.github/scripts)。

| 渠道 | 定义 | 本地构建入口 | 默认生成目录 |
| --- | --- | --- | --- |
| EXE | [inno/setup.iss](inno/setup.iss) | `ISCC.exe packaging/inno/setup.iss` | `packaging/inno/Output/` |
| MSIX | [msix/VirtualPaper.Package.wapproj](msix/VirtualPaper.Package.wapproj) | `pwsh -NoProfile -File .github/scripts/Build-Msix.ps1 -SkipSolutionBuild` | `packaging/msix/AppPackages/<本轮>/` |

EXE 编译需要 Inno Setup 6.7.1，示例假定 `ISCC.exe` 已加入 `PATH`。MSIX 需要 Visual Studio MSBuild 与 MSIX Packaging Tools；预览签名和商店身份参数见 [MSIX 指南](msix/README.zh-CN.md)。编译两种安装包都不会安装或启动应用。

两个渠道共用 [`localization`](../localization/README.zh-CN.md) 中的源文案。直接编译 ISS 时自动生成安装器资源，MSIX 工程在打包前生成清单。这些定义中的相对路径分别以 `inno/`、`msix/` 为起点。

生成的 `Output/`、`AppPackages/`、`bin/`、`obj/` 内容不提交。CI 入口 `.github/scripts/New-CIExePackage.ps1`、`.github/scripts/New-CIMsixPackage.ps1` 保持不变，默认将渠道产物写入 `dist/`。

两个安装包均注册 `.vpd`、`.vpw` 项目类型，显示可区分类型的项目图标，并提供“使用 VirtualPaper 打开”操作。`VirtualPaper.exe --open-project "<项目绝对路径>"` 启动 Core 或向已有实例转交请求，编辑区对已打开的项目激活原标签页，保留未保存修改。Inno 保留已有默认关联，卸载时仅清除自己的默认值；MSIX 关联遵循 Windows 应用选择。Inno 菜单文案使用安装语言，MSIX 文案遵循 Windows 的资源语言选择。

打包验证与安装后验收分别执行。`test/Run-Tests.ps1`、`test/Run-MsixTests.ps1` 的用法见[测试指南](../test/README.zh-CN.md)，CI 必需门禁见[工作流指南](../.github/workflows/README.zh-CN.md)。安装和真实 UI 执行需满足文档中的干净安装环境及桌面条件。
