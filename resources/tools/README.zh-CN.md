# 素材生成工具

[English](README.md) | [简体中文](README.zh-CN.md)

从仓库根目录手动运行这些工具。应用启动和常规构建不会调用它们。

| 工具 | 入口 | 用途 |
| --- | --- | --- |
| 内置壁纸 | [BuiltInWallpapers](BuiltInWallpapers/README.zh-CN.md) | 准备源素材，生成或校验壁纸元数据及预览。 |
| 应用图标 | [gen_appicon_ico.ps1](Icons/gen_appicon_ico.ps1) | 将源 PNG 转为多尺寸 ICO，需要 Windows 和 System.Drawing。 |

```powershell
pwsh -NoProfile -File resources/tools/Icons/gen_appicon_ico.ps1
```

依次输入源 PNG 路径和目标 ICO 路径。目标路径留空时，在源文件旁生成同名 ICO。

测试与验收入口仍在 [`test`](../../test/README.zh-CN.md)，工作流契约统一使用 [`.github/scripts/validate-test-workflow.ps1`](../../.github/scripts/validate-test-workflow.ps1)。根目录下被忽略的 `scripts/` 仅用于本地临时工具。
