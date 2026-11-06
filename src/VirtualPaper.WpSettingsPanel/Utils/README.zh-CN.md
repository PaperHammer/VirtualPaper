# WpSettingsPanel 索引与筛选

[English](README.md) | [简体中文](README.zh-CN.md)

## 数据流

```text
磁盘中的壁纸目录
    │  启动时并行扫描，按 CreateTime 降序排列
    ▼
WallpaperIndexFile.Items               完整元数据列表
    │
    ▼
WallpaperIndexService                  运行时索引
  _index.Items                         元数据条目
  _uidToIndex: Dictionary<UID, int>     UID → Items 中的位置
    │  Query(offset, limit)
    ▼
_libraryWallpapers                     已加载、未筛选的对象
    │  筛选只改变可见集合
    ▼
LibraryWallpapers                      UI 展示的 ObservableCollection
```

初次扫描按创建时间从新到旧排序。分页时才按需加载完整壁纸对象。筛选针对已加载对象，不会遍历磁盘上的全部元数据条目。

## 核心结构

### WallpaperIndexEntry

用于排序和搜索的轻量元数据快照，不持有完整的 `WpBasicData` 对象。

| 字段 | 用途 |
| --- | --- |
| `Uid` | 壁纸唯一标识，用作映射键 |
| `FolderPath` | 壁纸目录元数据 |
| `JsonPath` | `WpBasicData.json` 的路径，分页加载时读取 |
| `CreateTime` | 初始降序排列依据 |
| `Title` / `Author` / `Tags` | 搜索元数据，目前仅 Title 参与筛选 |

### WallpaperIndexService

- `_index.Items` 按索引顺序保存元数据条目。
- `_uidToIndex` 将 UID 映射到全局索引，支持 O(1) 字典查找。
- 删除条目或追加新条目时，通过 `RebuildUidIndexMapUnsafe()` 重建位置映射。

## 生命周期

### 初始化

```text
WallpaperIndexService.Initialize(dirs)
  └─ WallpaperIndexFile.RebuildIndexAsync(dirs)
       并行扫描壁纸目录
       读取 WpBasicData.json，创建 WallpaperIndexEntry 快照
       按 CreateTime 降序排列 Items
  └─ RebuildUidIndexMapUnsafe()
  └─ Initialized.TrySetResult(true)
```

### 增量加载

```text
LibraryContentsViewModel.InitContentAsync() / LoadMoreAsync()
  └─ _wallpaperIndexService.Query(_offset, _limit)
       Items.Skip(offset).Take(limit)
       读取每个条目的完整 WpBasicData
       LibraryWallpapers.Add(data)
       _libraryWallpapers.Add(data)   // 同一个对象引用
       _offset++
```

这里的位置假设是 `_libraryWallpapers[i]` 对应 `_index.Items[i]`。初始分页建立了这种对齐关系，更新时也需要维持；具体限制见下文。

### 新增与更新：UpdateLib

当前 ViewModel 实现的结构如下：

```csharp
if (_wallpaperIndexService.TryGetValue(data.WallpaperUid, out int idx)) {
    LibraryWallpapers[idx] = data;
    _libraryWallpapers[idx] = data;
}
else {
    // 将新导入的项目放到两个已加载集合的开头。
    LibraryWallpapers.Insert(0, data);
    _libraryWallpapers.Insert(0, data);
}
_wallpaperIndexService.Update(data);
```

服务会原位替换已有元数据，但将新条目追加到 `_index.Items` 的末尾。因此，仅在已加载集合的开头插入条目，并不能保持其与索引对齐。`UpdateLib` 还会发布壁纸导入面板事件，供预览、计数等功能使用。

### 删除：HandleDelete

```text
LibraryWallpapers.Remove(data)       从可见集合移除
_libraryWallpapers.Remove(data)      从已加载对象移除
_wallpaperIndexService.Remove(data)  移除元数据并重建 UID 映射
```

重建映射会更新删除后的全局位置。随后，筛选从剩余的已加载对象中生成结果。

## 筛选：IFilterable 与 FilterContext

### 接口

```text
IFilterable
  FilterKey FilterKeyword          标识使用方的筛选维度
  ApplyFilter(string keyword)      兼容入口：仅按标题筛选
  ApplyFilter(FilterContext ctx)   组合筛选条件
```

`FilterContext` 是承载当前条件的不可变 record，相关字段如下：

```text
TitleKeyword: string                  空字符串表示不筛选标题
ActiveTypes: IReadOnlySet<FileType>?   null 表示所有类型
```

可向上下文添加新条件，无需修改接口签名。各使用方读取自己支持的字段。

### 广播

```text
搜索框 / 类型选择
    ▼
WpSettingsViewModel.BroadcastFilter()
  创建 FilterContext { TitleKeyword, ActiveTypes }
    ├─► LibraryContentsViewModel.ApplyFilter(ctx)
    └─► 其他已注册的 IFilterable 使用方
```

使用方通过 `WeakReference<IFilterable>` 注册，订阅不会阻止垃圾回收。

### 有序同步

`ApplyFilter` 按标题和当前文件类型，从 `_libraryWallpapers` 中选择有序子集。LINQ `Where` 保持已加载列表的顺序。

1. 从后向前移除可见集合中不匹配的项目。保留项维持相对顺序，构成筛选结果的有序子序列。
2. 从头遍历筛选结果。若可见集合对应位置已经是同一个对象，则继续；否则，在该位置插入缺失对象。

由于第一阶段保持了保留对象的顺序，第二阶段无需 Move 操作。插入次数等于筛选结果数量减去保留项数量。这只限定了插入次数，不代表总运行时间的上界：成员检查和集合元素移动仍有各自的开销。

删除后，筛选结果来自最新的 `_libraryWallpapers`，因此能反映已移除对象。最终可见顺序遵循该已加载列表；是否与元数据索引一致，取决于前述位置假设。

## 类型分组

`WpTypeFilterItem` 提供四种分组：

| UI 分类 | FileType 值 |
| --- | --- |
| 静态图片 | `FImage` |
| 动态图片 | `FGif`, `FImageAI` |
| 视频 | `FVideo` |
| 交互式 Web | `FWebZip` |

`FDesign`、`FWebDesign` 和 `FUnknown` 没有对应分组，仅在未启用类型筛选时显示。

## 当前限制

| 限制 | 影响 |
| --- | --- |
| 筛选将 `_libraryWallpapers` 视为只读 | 筛选只修改 `LibraryWallpapers`。 |
| `UpdateLib` 对两个已加载集合使用全局索引 | 对应索引必须已加载，且可见集合的位置必须匹配。启用筛选后，可见索引可能不再正确。 |
| ViewModel 与索引服务对新项目的排序不同 | 已加载集合插入到开头，服务元数据追加到末尾。导入后全局位置可能不一致。 |
| 增量加载直接向两个集合添加项目 | 新加载项目会绕过已启用的筛选，直到重新应用筛选。 |
| 重新插入使用 ReferenceEquals | 两个集合必须共用同一批壁纸对象实例。 |
