# Core 入口 MSIX 打包

[English](README.md)

独立的 `VirtualPaper.Package.wapproj` 收集 Core 及现有 `Plugins/UI`、`Plugins/PlayerWeb`、`Plugins/ScrSaver`、`Plugins/ML`、`Plugins/Shaders`、`BuiltInWallpapers` 布局。开始菜单和开机启动入口都是 Core，再沿原生产路径启动子进程。WAP 工程不加入普通解决方案默认构建，现有 Inno 工作流继续服务 EXE 通道。

## 本地预览

需要 Windows、PowerShell 7.2+、Visual Studio MSBuild、MSIX Packaging Tools、Windows SDK 10.0.26100。在仓库根目录执行：

```powershell
pwsh -NoProfile -File .github/scripts/Build-Msix.ps1
# 已完成完整 Release 解决方案构建时：
pwsh -NoProfile -File .github/scripts/Build-Msix.ps1 -SkipSolutionBuild
```

脚本按原 Any CPU 映射重建 Release 解决方案，再为四个进程发布自包含 .NET／Windows App SDK 运行文件，装配 WinUI 资源、打包并检查实际产物。发布阶段把各进程构建输出重定向到 `Msix/obj`，避免自包含运行文件混入普通 EXE 输出。属性仅作用于本次调用；UI 工程旧的 `IsMsixRelease` 保持 false。每次产物使用 `Msix/AppPackages` 下的新目录。生成文件、安装包、证书不提交。

默认身份 `PaperHammer.VirtualPaper.Preview`、`CN=PaperHammer`、版本 `1.0.0.0` 仅用于本地预览。默认输出未签名包；侧载可传入 `-CertificateThumbprint '<40 位证书指纹>'`，并用 `-Publisher` 指定与现有受信任开发证书相同的主题。脚本不创建／信任证书，不安装、注册或启动应用。侧载还需准备清单声明的 `Microsoft.VCLibs.140.00.UWPDesktop` 框架；Web 播放需要测试机器上的 WebView2 Evergreen Runtime。

## 正式商店包

在 Partner Center 保留产品后，使用后台分配的准确身份：

```powershell
pwsh -NoProfile -File .github/scripts/Build-Msix.ps1 -Store `
  -IdentityName '<Package/Identity/Name>' -Publisher '<Package/Identity/Publisher>' `
  -PublisherDisplayName '<PublisherDisplayName>' -PackageVersion '1.0.1.0'
```

`-Store` 拒绝预览身份，要求显式提供发布者字段。包版本每段不超过 65535、首段非零、第四段为零，与现有 EXE 产品版本分别管理。身份参数只写入 `obj` 下的生成清单。商店会为通过审核的 MSIX 重新签名，提交无需购买签名证书。

## 运行边界和验收

- MSIX 停用 EXE／插件补丁执行，并正常释放插件启动锁。
- MSIX 在库启动前复制缺失的内置壁纸文件夹，已有文件夹整目录保留，与 Inno 一致；不引入用户数据／配置迁移。
- WinUI PRI 在包根目录合并，保留嵌入 XBF、字符串和 URI 名称，只把文件候选路径指向 `Plugins/UI`。使用微软的 [PRIINFO 索引器](https://learn.microsoft.com/en-us/windows/uwp/app-resources/makepri-exe-format-specific-indexers#priinfo)，安装后仍需验证 PRI 和 XAML。
- `test/Test-MsixPackage.ps1 -PackagePath '<application.msix>'` 检查实际包入口／启动任务、自包含运行文件、翻译、模型、Shader 和资源索引，不代表安装及 UI 验收通过。
- 提交前运行 WACK，并在干净机器验证普通用户开始菜单启动、Core/UI 通信、中英文、壁纸／编辑／AI／屏保、开机启动、升级后配置保留、正常退出、卸载。系统设置场景单独安排，不混入普通 UI 套件；不连接日常运行实例。Inno 冒烟和非打包 UI 测试不能替代 MSIX 验收。

当前 CI 仍只发布 Inno 包。MSIX 通道及安装验收通过并合入后，可从同一源码提交分别生成两个包，各自维护打包配置、包版本和验收证据。
