# 内置壁纸制作

[English](README.md)

打开 [`preview.html`](preview.html)，可在同一页查看实际视频、GIF 和交互式 Web 效果。

## 素材与权限

| 条目 | 作者 | 来源与条款 |
| --- | --- | --- |
| Alpine Stillness | VirtualPaper | 生成的高山湖泊，原始 1672×941 PNG 和深度缓存 |
| Emerald Morning | VirtualPaper | 生成的丘陵，原始 1672×941 PNG 和深度缓存 |
| Bigora Cascade | Termininja | [Bigora waterfall](https://commons.wikimedia.org/wiki/File:Bigora_waterfall.webm)，作者本人作品，CC0 1.0 |
| Santa Rosa Waves | Blervis | [Santa Rosa Island Waves](https://commons.wikimedia.org/wiki/File:Santa_Rosa_Island_Waves.webm)，作者本人作品，CC0 1.0 |
| Luminous Flow | VirtualPaper | 原创 Canvas 动画，GPL-3.0，参考 `src/WebBackdrop/Assets/templates/v1` |

两张静态图片由内置图像生成工具制作，不是真实地点的摄影记录，没有复用 README 截图中的 Unsplash 素材。[`artwork.json`](artwork.json) 保留完整提示词；原始 PNG 直接位于壁纸文件夹，不做放大。Web 壁纸不包含照片、说明面板或时钟。

GIF 和视频采用**两份不同的真实摄影素材**，于 2026-11-06 核对：各文件描述页均由创作者将其本人作品明确以 CC0 公开。[`media.json`](media.json) 记录作者、永久页面版本、下载地址、原始 SHA-256 和具体处理参数。大体积原片仅缓存到已忽略的 `TestResults/built-in-wallpapers/external`。

[CC0](https://creativecommons.org/publicdomain/zero/1.0/) 允许复制、修改、商用和再分发，包括单独分发媒体，不要求署名或相同许可。VirtualPaper 对这些媒体衍生文件及缩略图中自己可能持有的著作权也采用 CC0，不因应用代码的 GPL 许可给媒体附加限制。作者与来源说明用于追溯，不是额外使用条件。CC0 不保证第三方其他权利已获许可，也不提供担保。两个外部素材文件夹均附来源说明和[官方 CC0 全文](CC0-1.0.txt)。

## 准备与打包

先完成 Release 解决方案构建，准备 Python 3.11+、Pillow、websocket-client、Chrome/Edge、可编码 H.264 的 ffmpeg，以及项目现有的 Depth Anything V2 模型：

```powershell
# 原片不存在时下载两份固定的 CC0 素材，使用前检查 SHA-256。
python resources/tools/BuiltInWallpapers/prepare.py --ffmpeg 'C:/tools/ffmpeg.exe'
dotnet run --project resources/tools/BuiltInWallpapers/Generate.csproj -c Release -- 'E:/repos/codes/VirtualPaper'
# 只验证现有素材，不重新生成或下载：
dotnet run --project resources/tools/BuiltInWallpapers/Generate.csproj -c Release --no-build -- 'E:/repos/codes/VirtualPaper' --verify
# 只重新编码摄影素材并更新缩略图：
python resources/tools/BuiltInWallpapers/prepare.py --motion-only --ffmpeg 'C:/tools/ffmpeg.exe'
```

Bigora Cascade 采用原片第 3–9 秒，以固定的 1280×720 区域裁出旗帜下方的瀑布，缩小为 768×432，在循环接缝做 0.4 秒渐变。GIF 使用统一调色板及 20 fps 节奏；首尾相同帧共同占用一个 50 ms 间隔（20 + 30 ms），避免额外停顿。Santa Rosa Waves 使用 1080p 原片第 20–40 秒，循环接缝渐变 0.6 秒，编码为无音轨的 30 fps H.264。均按真实速度播放，不做光流插帧、生成运动、倒放水流或放大。

`prepare.py` 使用独立不可见浏览器和仅监听回环地址的服务器，检查 Luminous Flow 的真实鼠标光迹、点击扩散、暂停/恢复及减少动画行为，并由实际渲染生成缩略图。临时输入、浏览器配置及日志保留在已忽略的 `TestResults/built-in-wallpapers`。

`Generate.csproj` 直接使用现有 Release 程序集，不启动应用。它调用与 `WallpaperControl.CreateBasicData` 相同的图片/视频/Web Builder，替换为紧凑的列表缩略图，通过 `WpBasicData.SaveAsync` 保存元数据，并调用 `DepthMapCache.GetOrCreateAsync` 准备两张图片的深度缓存。校验正式索引能扫描到全部五项、元数据路径相对且不越出壁纸目录、作者/标签/许可完整、深度缓存能命中而不重新推理。反射仅用于 Core 内部 Builder，不修改生产 API。

所有条目附 `Built-in;内置` 标签。只有生成的静态图片标记 `AI-generated`；摄影素材使用 `CC0` 与原作者。目录名与显示标题对应，发布后应保持目录名及 UID 稳定。安装器会保留已有目录，若要发布新的替代壁纸，应使用新目录名。制作命令会覆盖仓库内已知输出，不修改用户已安装的壁纸库。
