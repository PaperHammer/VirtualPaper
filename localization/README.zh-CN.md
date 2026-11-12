# 共用语言资源

[English](README.md) | [简体中文](README.zh-CN.md)

`LanguageCatalog.json` 统一维护支持语种、本地显示名称、区域别名、安装器语言名称及 Inno 语言文件。默认语言为 `en-US`。当前包含英语、简体中文、繁体中文、日语、韩语、德语、法语、西班牙语、巴西葡萄牙语及俄语。各语种覆盖完整源 key，包含应用、托盘和安装器提示；实际长文本排版仍待真实 UI 检查，资源完整不代表布局已经验收。

每种语言的产品文案只维护在 `Strings/<locale>/Resources.resw` 中。现有 UI key 不变，`Core_`、`Common_FileAccess_`、`Installer_` 分别用于 Core／托盘、公共文件错误和安装器自定义提示。`ResourceMappings.xml` 将源 key 映射到现有运行时 key；XAML 属性映射仍在 [`XamlResources.xml`](../src/VirtualPaper.UIComponent/Localization/XamlResources.xml)。用户标题、作者、标签、项目内容、ID 及序列化枚举不随语言切换改写。

## 生成与构建

```powershell
pwsh -NoProfile -File localization/Generate-Resources.ps1 -OutputRoot TestResults/localization
pwsh -NoProfile -File localization/Generate-Resources.ps1 -OutputRoot TestResults/localization -Check
```

兼容 Windows PowerShell 5.1。`-Target UI|Core|Common|Installer|Manifest` 将单个消费者的资源直接生成到指定目录，默认 `All` 分别使用子目录；原 UI 生成命令保留为兼容入口。

`-Target Metadata` 为原生 WinUI 入口提供统一默认语言，不再额外复制一份 UI 翻译。

MSBuild 在项目各自的 `obj` 下生成 UI/Core/Common 资源。UI 沿用 WinUI3Localizer 和原有 `VirtualPaper.UIComponent/Strings` 运行目录。Core/Common 沿用 ResourceManager 名称及卫星程序集，英文作为中性资源，其他语种使用显式卫星资源。直接编译 ISS 时也会先生成忽略提交的 `obj/Localization` include。WAP 从语言清单生成真正的 MSIX manifest，再填入包身份与版本；仓库中的 manifest 为模板。

繁体中文 Inno 文件来自 [Inno 官方翻译页面](https://jrsoftware.org/files/istrans/)，为 GoneTone 及文件中列出的贡献者维护的原始文件，内容未作修改。原始下载文件的 SHA-256 为 `031684fc769259291fd563338b5abe20b7753c88ab5a2976b83a80788deb8455`，保留原作者声明，并附带 [Inno Setup 许可证](../InnoSetup/Languages/LICENSE-InnoSetup.txt)。其他新增安装语言使用 Inno Setup 自带的语言文件。

生成器拒绝重复 key／别名／映射、缺失的代码或 XAML 文案、格式占位符索引不一致、非空英文对应的空翻译、未登记的源语种目录，以及没有映射的静态 XAML UID。`-Check` 只校验，不修复文件。内容未变化时保留输出时间戳；仅清理已退役的生成资源，不删除无关文件或源文件。

## 语言选择

已有设置规范化后继续保留，升级不覆盖合法选择。仅首次创建或损坏配置恢复时，先读取安装语言标记，再依次匹配 Windows 首选语言。新安装器写入标准语言代码，读取时兼容旧 `english`／`chinese` 标记。`en-GB` 等区域语言可匹配支持的父语言；中文不会通过中性 `zh` 别名从繁体跨到简体。无法匹配及运行时文案缺失时回退英文。界面语言与区域日期／数字格式分开处理。

## 验证

```powershell
pwsh -NoProfile -File test/Test-LocalizationResources.ps1
pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1
```

契约只使用临时文件，覆盖所有生成目标、临时第三语种、无效输入、源目录保护、过期输出和退役资源清理。Core 单测覆盖语言匹配、首启持久化、升级保留和真实中性／卫星资源；产物测试逐项比对包内翻译与内嵌源快照，MSIX 同时检查实际语种声明。它们不能替代用户授权后的真实安装、UI 和布局验收。
