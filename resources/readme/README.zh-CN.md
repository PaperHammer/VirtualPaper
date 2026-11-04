# README 图片与动态预览

[English](README.md) | [简体中文](README.zh-CN.md)

- `hero.svg` 将产品名称、来自 `src/VirtualPaper.UI/Assets/icon-vp-512.png` 的官方透明应用图标，以及两张真实壁纸库截图组合在同一连续背景上。品牌区域的淡紫色逐渐过渡到深色窗口后的暮蓝色，下方配有柔和的浅青色高光。较大的亮色窗口位于深色窗口前方。透明度仅作用于背景，两张截图保持不透明，确保可读。所有图片均已嵌入，不依赖外部字体、图片或脚本。背景和排版为原创矢量设计，`download.svg` 和 `scene.svg` 也是如此。
- `snow.gif`、`fireflies.gif`、`meteors.gif` 和 `petals.gif` 在 `scene.svg` 上运行 `src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/js/effects/` 中的正式绘制逻辑。它们是效果演示，不是应用界面录屏，也不是 AI 图片动态化结果。
- 每个氛围 GIF 为 480×270，包含 64 帧，每帧 80 毫秒（5.12 秒，12.5 fps），循环播放以便查看。预览循环不代表生成的视频具备无缝循环能力。
- `parallax.gif` 使用 Emerald hills 和同一条循环五秒的鼠标轨迹，对比未修改的正式普通播放器（`default.html` / `js/default/run.js`，左侧）与深度播放器（`3d_depth_map.html` / `js/3d_depth/run.js`，右侧）。两个播放器先按真实桌面尺寸 1920×1080 渲染，再缩小用于对比；若直接按缩略图尺寸渲染，普通播放器的 CSS 透视会变弱。普通视差保留 1.05 倍缩放和 2 度倾斜；深度视差使用标准强度（1，着色器值 0.024），关闭自动漂移。指针表示输入的鼠标位置。这是播放器演示，不是应用窗口录屏。
- 视差 GIF 为 812×253，五秒循环，最高按 10 fps 采样。原生 RAF、深度平滑和 CSS 过渡按真实时间运行。浏览器帧使用 JPEG 质量 95，GIF 共用 256 色调色板。每帧时长遵循实际采集间隔，不会将较慢的采集加速播放。末帧与首帧完全相同，并将首帧时长分配到两端，避免额外停顿。不为预览修改播放器参数或着色器。
- `parallax-source.jpg` 与壁纸库使用同一张 1920×1080 Emerald hills 照片。`parallax-depth.png` 通过正式的 `DepthAnythingV2.LoadModel` / `Run` / `SaveDepthMap` 方法生成，使用 `depth_anything_v2_vits_dynamic.onnx`、默认 518 像素 FitLongestSide 预处理和 CPU FP32 推理。本目录保留两份输入，方便重新生成 GIF，无需再次下载图片或推理。未使用手绘深度图、额外图像变形或 AI 视频生成。
- `library-light-mica.jpg` 和 `library-dark-mica.jpg` 是 2026 年 11 月 4 日从当前本地 Release 应用采集的真实截图，尺寸为 1446×893。两者均使用英文界面、Mica 和同一个隔离的演示壁纸库。JPEG 保留截图工具输出的原始字节，未重绘应用控件、文字或缩略图。
- `editor-light-mica.jpg` 是 2026 年 11 月 4 日采集的英文图片编辑器真实截图，使用亮色模式和 Mica，尺寸为 1446×893。蓝色山湖场景直接使用应用的画笔和填充工具原创绘制，未使用图库照片或导入背景图。JPEG 保留截图工具输出的原始字节，截图后未修改绘画或应用控件。
- 合成仅涉及壁纸库截图的缩放、位置、周围背景和阴影。运行 `python resources/readme/compose-showcase.py`，可从两张壁纸库 JPEG 和官方图标重新生成 `hero.svg`。
- 风格迁移对比使用内置的 `demo/style_transfer/origin.jpg`、实际参考图 `preset_styles/watercolor.jpg` 和 `style-watercolor-result.jpg`。结果通过正式的 `AdaIn.LoadModel` / `RunAndSave` 方法生成，使用 `adain_style_transfer.onnx`，alpha 为 1.0，两张输入的短边均为 512 像素。它替换了未记录参考图的旧结果，未添加后期处理。
- `upscale-input-960.jpg` 来自 Ocean breeze 照片，通过 OpenCV `INTER_AREA` 从 1920×1080 下采样到 960×540，再以 JPEG 质量 95 保存。`upscale-result-4k.jpg` 使用该输入，通过正式的 `Realesrgan.LoadModel` / `RunAndSave` 方法和内置 `realesrgan_x4plus_dynamic.onnx` 模型生成，使用常规 CPU 实现、512 像素分块、16 像素重叠，输出为 JPEG 质量 95 的 3840×2160 图片。这是下采样示例，不表示能够精确还原原始照片细节。
- `upscale-detail-bicubic.png` 和 `upscale-detail-model.png` 是输出坐标 (2100, 1180) 处相同的 720×480 裁剪区域。基线使用 OpenCV `INTER_CUBIC`，将同一张解码后的 960×540 输入放大到 3840×2160，并在裁剪前以相同的 JPEG 质量 95 保存。两个裁剪图使用相同显示宽度，未额外添加模糊或锐化。主 README 提供完整 4K 模型结果的链接。

## 截图内的图片来源

演示壁纸库中展示的照片和抽象图片来自 [Unsplash](https://unsplash.com)，遵循 [Unsplash License](https://unsplash.com/license)。创作者保留其权利，这些图片不会按项目代码许可证重新授权。下载的原图保留在已忽略的本地暂存目录中，仅为复现视差渲染而保留的 Emerald hills 输入除外。本目录包含产品截图，以及用于视差和超分对比的 Emerald hills、Ocean breeze 衍生图片，并非应用素材包或壁纸下载合集。

| 展示名称 | 原始图片来源 |
| --- | --- |
| Alpine reflections | [原图](https://images.unsplash.com/photo-1470770841072-f978cf4d019e) |
| Northern lights | [原图](https://images.unsplash.com/photo-1531366936337-7c912a4589a7) |
| Ocean breeze | [原图](https://images.unsplash.com/photo-1507525428034-b723cf961d3e) |
| Midnight peaks | [原图](https://images.unsplash.com/photo-1519681393784-d120267933ba) |
| Forest light | [原图](https://images.unsplash.com/photo-1441974231531-c6227db76b6e) |
| Desert mesas | [原图](https://images.unsplash.com/photo-1509316785289-025f5b846b35) |
| Glass reverie | [Unsplash 上的 Yue Ma](https://unsplash.com/photos/abstract-soft-pink-and-blue-gradient-background-nPpQprSfKUI) |
| Alpine horizon | [原图](https://images.unsplash.com/photo-1506905925346-21bda4d32df4) |
| Coastal light | [原图](https://images.unsplash.com/photo-1518837695005-2083093ee35b) |
| Emerald hills | [原图](https://images.unsplash.com/photo-1501854140801-50d01698950b) |
| Layered mountains | [原图](https://images.unsplash.com/photo-1500534314209-a25ddb2bd429) |
| Celestial night | [原图](https://images.unsplash.com/photo-1419242902214-272b3f66ee7a) |

## 重新生成 GIF

Windows 上需要 Python 3.10 或更高版本、`Pillow`、`websocket-client`，以及已安装的 Chrome 或 Edge：

```powershell
python resources/readme/generate-previews.py
# 可选：指定浏览器路径。
python resources/readme/generate-previews.py --browser "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe"
# 使用已保存的源图和深度图生成视差对照：
python resources/readme/generate-parallax-preview.py
```

脚本使用独立的无头浏览器配置目录，以及仅监听回环地址的 HTTP 服务，不访问用户的浏览器配置，也不启动 VirtualPaper。随机种子、强度、尺寸和时间步长固定，以便重复生成预览。共用的 GIF 调色板用于避免静态背景逐帧变色。

这些脚本仅向本目录写入 GIF。临时浏览器配置、预览拼图和视差渲染检查保存到已忽略的 `TestResults/readme-refresh-20261104/` 目录。正式效果或深度播放器变化后，请重新运行对应脚本，并检查动画和文件大小。
