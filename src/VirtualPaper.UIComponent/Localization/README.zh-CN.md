# 语言资源

[English](README.md) | [简体中文](README.zh-CN.md)

每种语言的文案只维护在 `../Strings/<locale>/Resources.resw` 中。这些源 key 供 `LanguageUtil.GetI18n` 使用，不要在源文件中添加 `Ui_...` 条目。

`XamlResources.xml` 统一描述 XAML 属性资源与源 key 的映射，不区分语言：

```xml
<Resource Name="Ui_Intelligent_Result_PreservationTooltip.ToolTipService.ToolTip"
          Source="Intelligent_Result_PreservationTooltip" />
```

XAML 中的 UID 为 `Ui_Intelligent_Result_PreservationTooltip`，后缀指定需要更新的属性。同一控件的多个属性使用相同 UID，但分别配置映射。

## 修改与构建

1. 修改文案时，只修改各语言源文件的 value，已有 XAML 映射无需修改。
2. 新增本地化控件或属性时，添加源 key 及翻译、添加一条映射，并在 XAML 中设置 `l:Uids.Uid`。
3. 正常构建解决方案即可。`GenerateLocalizedResources` 在内容路径分配前执行，覆盖 Visual Studio 和两个 CI 工作流。完整资源生成在 `obj/<configuration>/<target-framework>/Localization/Strings` 下，再复制到原有运行目录；生成文件不纳入 Git。

生成器会拒绝重复 key 或映射、缺失的映射源文案、源文件中手工添加的 `Ui_...` 条目，以及没有映射的静态 XAML UID。扫描会排除构建输出目录。内容未变化时，不更新生成文件的时间戳。

在仓库根目录单独生成或校验输出：

```powershell
powershell -NoProfile -File src/VirtualPaper.UIComponent/Localization/Generate-Resources.ps1 -OutputRoot TestResults/localization-resources
powershell -NoProfile -File src/VirtualPaper.UIComponent/Localization/Generate-Resources.ps1 -OutputRoot TestResults/localization-resources -Check
```

脚本兼容 Windows PowerShell 5.1 和 PowerShell 7。`-Check` 只核对已有生成结果，不写入文件。输出位置必须在源 `Strings` 目录之外。

## 包体校验

`test-artifacts` 用测试程序集内嵌的映射快照核验包内完整语言资源：所有映射必须存在、生成文案必须等于源文案、生成属性数量必须与映射数量一致。运行包只需要生成后的资源文件。生成的 `Source:` 注释仅用于阅读，测试不再依赖注释。

这些检查覆盖生成和打包。语言即时切换仍由真实 WinUI 语言用例验证，资源校验不能替代真实交互验收。
