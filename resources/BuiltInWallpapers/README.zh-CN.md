# 内置壁纸

[English](README.md)

在此放置已经由 VirtualPaper 导入处理完成的完整壁纸文件夹，每份壁纸使用固定目录名：

```text
BuiltInWallpapers/
  <wallpaper-id>/
    wp_metadata_basic.json
    ...媒体、缩略图、深度缓存及效果文件...
```

使用格式 19 和相对路径，保持目录名及 WallpaperUid 稳定，引用的资源全部放在对应壁纸文件夹内。3D 图片保留生成的 `depth-<hash>.png` 文件名；原图或模型变化后可能重新生成深度图。其他机器的显示器运行配置不能代替用户实际显示器的配置。

构建时复制到 Core 输出目录的 `BuiltInWallpapers/` 下，不复制这两份 README，也兼容没有素材的空目录。

## 当前素材

| 标题 | 类型 | 内容 |
| --- | --- | --- |
| Alpine Stillness | 图片 / 3D 图片 | 生成的高山湖泊，附预生成深度缓存 |
| Emerald Morning | 图片 / 3D 图片 | 生成的晨雾丘陵，附预生成深度缓存 |
| Bigora Cascade | GIF | Termininja 拍摄的真实瀑布，CC0 |
| Santa Rosa Waves | 视频 | Blervis 拍摄的真实 1080p 海岸，CC0 |
| Luminous Flow | Web | 参考默认 Web 模板的鼠标光迹及点击扩散 |

各项附加 `Built-in;内置` 标签，填写完整标题、描述和日期。两张静态图片为 AI 生成风景，作者填写 VirtualPaper，保留 1672×941 原始 PNG 和预生成深度缓存，没有包含 README 截图里的 Unsplash 素材。Luminous Flow 为原创 Canvas 代码，不含照片。

GIF 与视频使用两份不同的真实摄影素材。Bigora Cascade 裁切为 768×432、20 fps GIF；Santa Rosa Waves 为无音轨的 1920×1080、30 fps H.264。按真实速度播放，仅在循环接缝做短暂渐变。保留真实作者与 `CC0` 标签，不标记为 `AI-generated`。原作者均明确将自己的素材按 CC0 公开：[Bigora waterfall](https://commons.wikimedia.org/wiki/File:Bigora_waterfall.webm)、[Santa Rosa Island Waves](https://commons.wikimedia.org/wiki/File:Santa_Rosa_Island_Waves.webm)。CC0 允许商用、修改和再分发，也允许独立分发媒体，不要求署名或以相同许可分享；VirtualPaper 不为这些媒体衍生文件附加限制。CC0 不保证第三方其他权利已获许可，也不提供担保。

每个文件夹附 `NOTICE.txt`，外部摄影素材另附完整 CC0 文本。[制作说明](../tools/BuiltInWallpapers/README.zh-CN.md)、来源地址、固定源文件哈希和处理参数单独保留，不随安装包部署。原始下载位于已忽略的 `TestResults`，避免将大体积原片重复放入 Git。

完整安装包在安装及覆盖更新时，将新文件夹部署到 `user_settings.json` 中保存的 `BuiltInWallpapersDir`。默认位置为 `<壁纸库根目录>/BuiltInWallpapers`，与 `<壁纸库根目录>/wallpapers` 并列；默认根目录为 `%LOCALAPPDATA%\VirtualPaper\Library`。旧配置缺少该字段时，应用与安装器均根据 `WallpaperDir` 推导默认位置。已经存在的壁纸文件夹整体跳过，保留用户修改和文件夹内删除的文件；如果删除整个壁纸文件夹，下次安装可以补回。卸载保留这些文件，除非用户选择删除全部应用数据；已移出应用数据目录的壁纸库仍保留。

设置页仍只显示一个目录入口。更改位置时，将两个目录迁移到所选根目录下的 `wallpapers` 和 `BuiltInWallpapers`，同步更新播放布局、运行配置并保存两个路径。即使内置目录原先配置在其他位置，也会一起迁移。所选根目录可以包含其他文件，但不能位于源库内部。复制入口拒绝占用的库子目录，并禁止覆盖已有文件。重新选择当前根目录且所有库路径均未变化时直接返回。准备或恢复播放失败时保留原数据。

任何文件复制开始前，复用源目录扫描结果计算各库的最长相对路径，拼接库目录名后得到的完整目标路径须处于应用规定的 259 字符迁移兼容上限内。这是针对尚未统一验证长路径行为的系统外壳及原生组件的保守策略，不是 .NET 的路径上限。参阅微软的 [Win32 路径说明](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation)。

复制后仅检查目录条目和文件大小，不重新读取文件内容。复制前记录源目录条目、文件大小和修改时间，复制后检测到源数据变化时停止准备。所有库都准备完成后才切换路径；配置保存且恢复播放返回成功后，仅清理本轮实际复制过的源目录，并再次比较源目录记录。检测到源变化时保留旧目录；删除失败记录日志，旧位置可能残留部分文件。清理不再固定等待或盲目重试。这是基于元数据的变化检查，不保证文件内容完整性，也不提供崩溃及最终检查之后并发写入的事务保护。

壁纸库同时扫描此目录和用户设置的壁纸目录，沿用现有预览、播放、编辑和删除操作，无需启动导入或下载。仅插件更新不部署此目录，新增内置壁纸需要完整安装包。

应用侧的壁纸库目录统一登记在 [`WallpaperLibraryDirectories`](../../src/VirtualPaper.Models/Cores/WallpaperLibraryDirectories.cs)，提供目录名及配置读写方式，供扫描、迁移和回退共用。新增目录还需补齐配置/RPC 字段、默认值及相应的安装包资源处理。
