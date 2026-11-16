# 素材生成工具

[English](README.md) | [简体中文](README.zh-CN.md)

从仓库根目录手动运行这些工具。应用启动和常规构建不会调用它们。

| 工具 | 入口 | 用途 |
| --- | --- | --- |
| 内置壁纸 | [BuiltInWallpapers](BuiltInWallpapers/README.zh-CN.md) | 准备源素材，生成或校验壁纸元数据及预览。 |
| 商店材料 | [发布材料说明](../store/README.zh-CN.md) | `Start-StorePreview.ps1` 启动隔离演示；`Capture-StoreScreenshot.ps1` 导出无指针原生 PNG；`Export-StoreMaterials.ps1` 校验／导出多语言文案及图片。这些本地工具不上传或发布。 |
| 商店 Hero 与首图 | [Generate-StoreArtwork.py](Generate-StoreArtwork.py) | 使用独立无头 Chrome／Edge 合成柔焦 4K Hero，并将现有 README Hero 导出为首张截图。需要 Python、Pillow 和 websocket-client。 |
| 应用品牌素材 | [Generate-BrandAssets.ps1](Icons/Generate-BrandAssets.ps1) | 从统一的[矢量图标](../icons/virtualpaper.svg)和 [Logo 源素材](../logos/README.md)生成应用／安装器 ICO、MSIX 图片、下载徽标、启动图、Logo 和 README 品牌图标。需要 Windows 和 STA PowerShell。 |
| 商店图标 | [Generate-StoreIcons.ps1](Icons/Generate-StoreIcons.ps1) | 使用同一矢量源导出透明版和蓝紫底板版，尺寸为 300／512／1024 像素；完整品牌素材生成时会一并执行。 |
| PNG 转 ICO 工具 | [gen_appicon_ico.ps1](Icons/gen_appicon_ico.ps1) | 交互式转换其他 PNG 素材；应用品牌素材使用上方的矢量生成器。 |
| 项目文件图标 | [Generate-ProjectFileIcons.ps1](Icons/Generate-ProjectFileIcons.ps1) | 将自包含的 `.vpd`／`.vpw` SVG 源图渲染为 PNG 和七种尺寸的 ICO。字标已转为轮廓，渲染时无需字体。在 Windows 上执行 `pwsh -NoProfile -STA -File resources/tools/Icons/Generate-ProjectFileIcons.ps1`。 |

```powershell
pwsh -NoProfile -STA -File resources/tools/Icons/Generate-BrandAssets.ps1
```

修改 SVG 源文件后重新生成即可。保留原有路径和像素尺寸，无需安装字体或 Python 图像库。

测试与验收入口仍在 [`test`](../../test/README.zh-CN.md)，工作流契约统一使用 [`.github/scripts/validate-test-workflow.ps1`](../../.github/scripts/validate-test-workflow.ps1)。根目录下被忽略的 `scripts/` 仅用于本地临时工具。
