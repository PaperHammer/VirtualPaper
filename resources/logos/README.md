# Logo sources / Logo 源素材

The application icon master is [`../icons/virtualpaper.svg`](../icons/virtualpaper.svg).
Its smooth curves and gradient folds are shared by the application, installer,
MSIX and Store presentations. PNG and ICO files are exports, not input sources.
All ICO exports contain 32, 40, 48, 64, 128 and 256 px frames from the same master;
16/24 px frames are not retained. Existing ICO filenames are preserved.

应用图标母版为 [`../icons/virtualpaper.svg`](../icons/virtualpaper.svg)。应用、安装器、
MSIX 和商店使用同一组平滑曲线、渐变和折面。PNG、ICO 为导出结果，不再作为生成源。
所有 ICO 均从同一母版生成 32、40、48、64、128、256 像素帧，
不保留 16／24 像素帧，并沿用原有 ICO 文件名。

`source/virtual.svg` and `source/paper.svg` preserve the existing wordmark as
outlines, without font dependencies. `source/layouts.json` records the existing
canvas dimensions and icon/word positions. Background logos retain the original
pale-blue gradient; transparent logos keep their alpha channel. The `_with_round40`
versions keep 40-pixel corner clipping. Historical filenames are retained:
`virtualpaper-logo_4380x712_*` and `virtualpaper-logo_4K*` are actually 4208 × 712 px.

`source/virtual.svg`、`source/paper.svg` 将原有字标保存为矢量轮廓，无需依赖字体。
`source/layouts.json` 记录原有画布尺寸及图文位置。有底色的 Logo 保留原有浅蓝渐变，
透明版保留透明通道，`_with_round40` 版本保留 40 像素圆角。沿用历史文件名：
`virtualpaper-logo_4380x712_*`、`virtualpaper-logo_4K*` 的实际尺寸为 4208 × 712 像素。

Regenerate on Windows / 在 Windows 上重新生成：

```powershell
pwsh -NoProfile -STA -File resources/tools/Icons/Generate-BrandAssets.ps1
```

The command also refreshes consumer copies under `src`, Inno images, Store
artwork, and only the header icon embedded in `resources/readme/hero.svg`.
The real application captures remain unchanged. Review exports on light/dark
backgrounds and inspect small ICO sizes before committing.

命令会同步更新 `src` 中的资源副本、Inno 图片、商店图标，以及
`resources/readme/hero.svg` 内嵌的顶部图标，保留真实应用截图。
提交前应检查亮暗背景及小尺寸 ICO 的显示效果。
