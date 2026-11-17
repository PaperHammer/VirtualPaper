# Microsoft Store 发布材料

[English](README.md) | [简体中文](README.zh-CN.md)

这里统一维护已审查的商店文案与截图。Pre-release 从本轮固定源码快照读取这些材料，与验收通过的 MSIX 一起上传到 Partner Center **未提交草稿**。

## 内容放在哪里

| 内容 | 源文件 | 维护规则 |
| --- | --- | --- |
| 当前简介、详情、功能、关键词、截图顺序与说明 | `listings/<locale>.json` | 修改对应语种，始终保留语言清单中的全部语种。 |
| 每版更新说明 | `releases/<产品版本>.json` | 使用版本标签计算出的四段产品版本，包含全部语种；不是 MSIX 包版本。 |
| 真实界面图片 | `screenshots/<locale>/<NN-scene>.png` | 界面变化时替换原场景文件；未变化的图片可以沿用。 |
| 应用图标版本 | [`artwork/`](artwork/README.md) | 从应用统一矢量源导出的透明版／圆角底板版 SVG，以及 300／512／1024 像素 PNG；不含文字。 |
| 商店 Hero 背景 | `artwork/store-hero-3840x2160.png` | 无文字、带柔焦朦胧感的 4K 16:9 背景及 SVG 合成源，与截图列表分开维护。 |
| 场景引用、共用英文图片、采集来源 | `screenshots/index.json` | 新场景在这里登记，并为各语种条目补充同一场景 ID、说明和顺序。 |
| 支持语种 | [`localization/LanguageCatalog.json`](../../localization/LanguageCatalog.json) | 新语种同时补文案、更新说明及图片映射，或明确复用英文图。 |
| 商店公开标识 | [`packaging/msix/StoreIdentity.json`](../../packaging/msix/StoreIdentity.json) | 身份验证密钥不放入仓库。 |

发布 PR 合并前，准备好 Pre-release 将计算出的版本对应的更新说明。仅插件发布沿用产品版本，读取该版本的说明。文案、翻译和图片随代码一起审查，流水线不临时翻译或自动采集截图。

当前 `0.6.1.0` 材料包含十种语言的详情及更新说明，每种语言七条截图说明，共 21 张截图 PNG。各语种首图为 `00-overview`，由 [`resources/readme/hero.svg`](../readme/hero.svg) 按原有布局导出为 1920 × 1200，保留其中的品牌元素及英文亮暗主题真实壁纸库画面。其后六个场景保持不变：中英文各六个场景，其他八种语言各一张壁纸库画面，其余复用英文图片。共用英文图不代表该图片中的界面已经翻译。

## 图片标准与采集

功能截图使用真实 Release 窗口，PNG，至少 1366 × 768，每张不超过 50 MB。项目约定每个语种 4–10 个场景（当前七个），截图说明不超过 200 字符。保留原始像素，不放大小截图。功能截图不得出现鼠标指针、光标高亮、桌面通知、日常壁纸库、账号、私人路径或额外宣传叠字。首图为单独标记的现有 README 合成图，其源文件、哈希与原始采集日期在 `screenshots/index.json` 对应场景中记录。

功能截图与新 Hero 使用内置的 VirtualPaper 生成素材或 CC0 素材，授权见 [`resources/BuiltInWallpapers`](../BuiltInWallpapers) 中的声明。首图保留 README 中原有的真实截图，其 Unsplash 素材来源和权利说明见 [README 素材说明](../readme/README.zh-CN.md)。采集来源记录在 `screenshots/index.json`。

无需占用前台，重新生成 Hero 和首图：

```powershell
python resources/tools/Generate-StoreArtwork.py
```

需要 Python 3.10+、Pillow、websocket-client 和 Chrome 或 Edge。工具在 `TestResults` 下使用独立的无头浏览器配置，不修改 `hero.svg`，导出首图后更新其来源哈希。修改 README Hero 或重新生成品牌素材后，需再次执行。Hero 不含文字或应用 UI，使用蓝紫薄雾、内置壁纸及半透明纸张折面，与图标一起保存在 `artwork` 下，对应商店的 **16:9 Super hero art** 字段；当前草稿自动化仍保留已有商店品牌素材。参见[官方图片说明](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/screenshots-and-images)。

先完整编译 Release，再在已解锁桌面启动一次性演示配置：

```powershell
$preview = & resources/tools/Start-StorePreview.ps1 -Locale zh-CN
# 等待 UI 子进程出现，再查询 PID。
$uiId = (Get-CimInstance Win32_Process -Filter "ParentProcessId=$($preview.CoreId)" |
    Where-Object Name -EQ 'VirtualPaper.UI.exe').ProcessId
# 在真实界面调整场景，等待加载和动画结束后：
& resources/tools/Capture-StoreScreenshot.ps1 -ProcessId $uiId `
    -Destination resources/store/screenshots/zh-CN/01-library.png
# 次级窗口示例：额外指定 -Title 'Alpine Stillness (Preview)'。
```

演示使用真实 Core/UI、隔离数据与命名管道、内置壁纸和示例项目，不执行安装或卸载。完成后只关闭本轮演示 UI 及其 Core。采集工具复用测试中的 `PrintWindow` 导出，不包含鼠标指针，仅裁掉 DWM 不可见边框，不缩放、不修图。逐张检查输出，尤其是 WebView／GPU 内容。带有指针或光标高亮的屏幕采集结果不能直接作为最终材料。

## 离线审查与交接

```powershell
pwsh -NoProfile -File resources/tools/Export-StoreMaterials.ps1 -Version 0.6.1.0 -InitialRelease
```

输出位于 `TestResults/store-materials/`，包含各语种可阅读 Markdown、`listings.json`、图片和哈希清单，不登录或上传。首发使用 `-InitialRelease`，让商店“新增功能”留空，可阅读文档仍保留版本说明；后续更新省略该参数。

`listings.json` 是部分条目数据，不是完整提交或 Partner Center CSV 导入模板。导出图片名包含内容哈希，仓库中的场景文件名保持稳定。`SourceSha` 表示本次发布输入快照，`ScreenshotSourceSha` 表示截图所用构建。旧图仍准确代表当前界面时可以继续使用。

## Pre-release 如何消费

已检查的 PR 带有精确的 `channel:msstore` 标签时，准备阶段检查对应产品版本的说明、全部语种文案与图片。未带此标签时，跳过商店配置校验、MSIX 打包／安装验收及 Store draft job。全部必需的打包／测试验收和版本同步通过后，`Draft Release (GitHub)` 与 `Draft Release (Microsoft Store)` 并行执行，彼此没有依赖。`draft-msstore` 从同一固定快照导出材料，将已验收的 `.msixupload` 与引用的图片放进同一 ZIP 上传，并将可阅读导出保留为 `store-materials` 产物。

对维护中的各语种，更新简介、详情、功能、关键词、更新说明及有序桌面截图集。截图集以仓库为准，过时的桌面截图会从该语种草稿移除；内容未变化的已上传图片复用原 ID。保留商店名称、其他语种、平台覆盖、Logo、法律字段、声明、价格和其他架构包。新增语种沿用已有的保留产品名。

草稿归属标记绑定 workflow run、源码、安装包哈希和材料哈希。同一失败 job 可续传它自己的 `PendingCommit` 草稿；材料变化时会在更新前拒绝重用。修改文案／图片需新的已审查源码快照和发布运行，旧快照 rerun 不会读取新内容。上传后再次读取 API，核对维护字段和图片顺序。`store-draft.json` 记录哈希及 `Committed=false`，不记录密钥或上传地址。

流水线不提交商店草稿、不请求认证、不上架。API 创建的草稿继续通过 API 管理，避免混用门户编辑同一提交。年龄分级、市场、价格、支持方式及公开隐私政策网址仍单独在 Partner Center 配置。

运行 `pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1` 可验证离线材料、工作流及 HTTP 模拟契约，不连接 Partner Center，不采集界面或安装应用。

规范来源：[商店字段](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/add-and-edit-store-listing-info)、[截图要求](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/screenshots-and-images)、[提交 API](https://learn.microsoft.com/windows/uwp/monetize/manage-app-submissions)。
