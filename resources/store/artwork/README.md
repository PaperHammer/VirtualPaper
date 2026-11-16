# Store artwork / 商店品牌素材

Icon presentations are derived from the canonical smooth vector icon at
[`../../icons/virtualpaper.svg`](../../icons/virtualpaper.svg). Its curves retain
the original wing silhouette and two shaded folds. There are no embedded
bitmaps, fonts or lettering. Every PNG is rendered directly at its target size.

所有版本都来自统一的[平滑矢量图标](../../icons/virtualpaper.svg)，保留原有羽翼轮廓与
两处阴影折面，不嵌入位图、字体或文字。各尺寸 PNG 均从矢量源直接渲染。

| Variant / 版本 | SVG export / SVG 导出 | PNG sizes / 尺寸 |
| --- | --- | --- |
| Transparent / 透明 | `app-icon-transparent.svg` | 300, 512, 1024 px |
| Rounded tile / 圆角底板 | `app-icon-tile.svg` | 300, 512, 1024 px |

The tile uses a deeper blue-to-violet background (`#4669A8` → `#6B50A0`).
Use the 300 px image for Store artwork upload; 512/1024 px exports support larger
previews. The submission workflow preserves existing Store logos, so artwork
uploads remain separate from localized screenshots.

底板采用较深的蓝紫渐变（`#4669A8` → `#6B50A0`）。商店图标上传使用 300 像素版本，
512／1024 像素供高清预览。提交流程保留已有商店 Logo，图标上传与多语言截图分开处理。

Regenerate Store exports on Windows / 在 Windows 上重新生成商店图标：

```powershell
pwsh -NoProfile -STA -File resources/tools/Icons/Generate-StoreIcons.ps1
```

To refresh all application, installer and logo assets too, run
`resources/tools/Icons/Generate-BrandAssets.ps1` instead. Edit the canonical SVG,
not these generated copies. See the [logo source guide](../../logos/README.md).

如需同时更新应用、安装器和 Logo，请运行 `resources/tools/Icons/Generate-BrandAssets.ps1`。
后续只修改统一的 SVG 母版，再重新生成，避免从导出的 PNG 反向描摹。
参见 [Logo 源素材说明](../../logos/README.md)。

## Hero background / Hero 背景

`store-hero-3840x2160.png` is the 4K upload asset for the Store's 16:9 Super hero
art field. It is rendered from the `store-hero.svg` composition: soft-focus
blue/violet wallpaper layers, mist and
translucent folds, without text, logos or application UI. The left area keeps
low contrast for Store-provided title overlays. This is original product artwork;
the Lively reference is not included or reused.

`store-hero-3840x2160.png` 为商店 16:9 Super hero art 的 4K 上传素材，
由 `store-hero.svg` 渲染：蓝紫色的柔焦壁纸层、
薄雾与半透明折面，无文字、Logo 或应用 UI；左侧保持低对比，方便商店叠加标题。
合成为原创设计，不包含或复用 Lively 参考图。

Inputs are `vp-alpine-stillness.png`, `vp-emerald-morning.png` and the static
`vp-luminous-flow/preview.gif` under `resources/BuiltInWallpapers`; their existing
notices apply. The SVG embeds compressed copies for standalone viewing.
The generator also rasterizes `resources/readme/hero.svg` unchanged into the
first screenshot, `screenshots/en-US/00-overview.png` (1920 × 1200).

输入素材为 `resources/BuiltInWallpapers` 下的 `vp-alpine-stillness.png`、
`vp-emerald-morning.png` 及静态 `vp-luminous-flow/preview.gif`，沿用各自已有的声明。
SVG 内嵌压缩副本，可独立查看。生成器还会将现有 `resources/readme/hero.svg`
按原样渲染为首张截图 `screenshots/en-US/00-overview.png`（1920 × 1200）。

```powershell
python resources/tools/Generate-StoreArtwork.py
```

Requires Python 3.10+, Pillow, websocket-client and Chrome/Edge; no foreground
interaction or application launch. The current Store draft uploader preserves
logos/hero artwork; this background remains separate from its screenshot patch.

需要 Python 3.10+、Pillow、websocket-client 及 Chrome／Edge，不占用前台或启动应用。
当前商店草稿上传逻辑保留 Logo／Hero 品牌素材，这张背景与截图更新分开使用。
