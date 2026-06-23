# WpSettingsPanel — 索引与过滤机制说明

## 1. 总体架构

```
磁盘壁纸目录
    │  (启动时并行扫描)
    ▼
WallpaperIndexFile.Items          ← 全量有序列表 (List<WallpaperIndexEntry>)
    │                                排序规则：CreateTime 降序
    │  UID → 全局位置
    ▼
WallpaperIndexService             ← 运行时索引服务
  · _index.Items                  ← 同 WallpaperIndexFile.Items
  · _uidToIndex: Dict<UID,int>    ← UID → 在 Items 中的全局位置

    │  按 offset/limit 分页加载
    ▼
_libraryWallpapers (List<T>)      ← 内存已加载项，位置与 _index.Items[0..offset-1] 对齐
    │
    │  过滤（只移除/插入，不修改 _libraryWallpapers）
    ▼
LibraryWallpapers (ObservableCollection<T>)  ← UI 可见项（filtered 子集）
```

---

## 2. 核心数据结构

### `WallpaperIndexEntry`
轻量级磁盘元数据快照，仅含用于排序/搜索的字段，不持有完整 `WpBasicData`。

| 字段 | 用途 |
|------|------|
| `Uid` | 全局唯一标识，作为所有映射的 Key |
| `FolderPath` | 壁纸目录，用于 Remove 定位 |
| `JsonPath` | `WpBasicData.json` 路径，供分页加载时懒读取 |
| `CreateTime` | **全局排序依据**，降序（最新在前） |
| `Title` / `Author` / `Tags` | 预置搜索字段（当前仅 Title 参与过滤） |

### `WallpaperIndexService`
- `_index.Items`：`WallpaperIndexEntry` 的有序列表，顺序即为最终展示顺序。
- `_uidToIndex`：UID → 全局位置的字典，所有按位置操作的 O(1) 入口。
- 删除/更新后调用 `RebuildUidIndexMapUnsafe()` 重建映射，保证位置始终与列表一致。

---

## 3. 生命周期

### 3.1 初始化
```
WallpaperIndexService.Initialize(dirs)
  └─ WallpaperIndexFile.RebuildIndexAsync(dirs)
       并行扫描各壁纸子目录 → 读 WpBasicData.json → 构建 WallpaperIndexEntry
       最终 Items 按 CreateTime 降序排列
       设置 Initialized.TrySetResult(true)
```

### 3.2 分页加载（增量）
```
LibraryContentsViewModel.InitContentAsync() / LoadMoreAsync()
  └─ _wallpaperIndexService.Query(_offset, _limit)   // Items.Skip(offset).Take(limit)
       对每条 entry 懒加载完整 WpBasicData
       LibraryWallpapers.Add(data)       ← 同一个对象引用
       _libraryWallpapers.Add(data)      ← 同一个对象引用
       _offset++
```

> **位置不变式**：`_libraryWallpapers[i]` 的数据与 `_index.Items[i]` 完全对应，
> 索引 `i` 在两者中含义相同。

### 3.3 新增 / 更新 (`UpdateLib`)
```csharp
if (_wallpaperIndexService.TryGetValue(uid, out int idx))
    // 更新已有位置
    LibraryWallpapers[idx] = data
    _libraryWallpapers[idx] = data
else
    // 新条目插入列表头（最新在前）
    LibraryWallpapers.Insert(0, data)
    _libraryWallpapers.Insert(0, data)
_wallpaperIndexService.Update(data)
```

### 3.4 删除 (`HandleDelete`)
```csharp
LibraryWallpapers.Remove(data)        // 从 UI 集合中移除
_libraryWallpapers.Remove(data)       // 从内存列表中移除
_wallpaperIndexService.Remove(data)   // 从 _index.Items 移除，重建 _uidToIndex
```
三者同步移除后，`_wallpaperIndexService.RebuildUidIndexMapUnsafe()` 让后续位置均下移一位，保持对齐。

---

## 4. 过滤机制 (`IFilterable / FilterContext`)

### 4.1 接口设计

```
IFilterable
  FilterKey FilterKeyword          ← 标识该消费者关注的过滤维度
  ApplyFilter(string keyword)      ← 向后兼容快捷入口（仅标题）
  ApplyFilter(FilterContext ctx)   ← 组合过滤入口
```

`FilterContext` 是不可变 record，承载所有激活的过滤条件：

```csharp
record FilterContext {
    string TitleKeyword    // 空字符串 = 不过滤
    IReadOnlySet<FileType>? ActiveTypes  // null = 接受所有类型
}
```
> **扩展性**：新增过滤维度只需在 `FilterContext` 添加字段，
> 不改动接口签名，消费者按需读取新字段即可。

### 4.2 广播路径

```
用户操作（搜索框 / 类型勾选）
    │
    ▼
WpSettingsViewModel.BroadcastFilter()
  构造 FilterContext { TitleKeyword, ActiveTypes }
    │
    ├─► IFilterable.ApplyFilter(ctx)   (LibraryContentsViewModel)
    └─► IFilterable.ApplyFilter(ctx)   (其他注册消费者，弱引用)
```

消费者以 `WeakReference<IFilterable>` 注册，不阻止 GC。

### 4.3 `ApplyFilter` 的两阶段有序同步算法

```
filtered = _libraryWallpapers.Where(TitleKeyword && ActiveTypes)
         = 原始有序子集（LINQ Where 对 List 是顺序枚举，保序）

Phase 1 — 移除不匹配项（维护剩余项的相对顺序）
  for i = Count-1 downto 0:
    if LibraryWallpapers[i] ∉ filtered:
      LibraryWallpapers.RemoveAt(i)    // 倒序保证低位索引稳定

  后置条件：LibraryWallpapers ⊆ filtered，且相对顺序与 filtered 一致
            即 LibraryWallpapers 是 filtered 的有序子序列

Phase 2 — 插入缺失项（维护与 _libraryWallpapers 相同的最终顺序）
  for i = 0 to filtered.Count-1:
    if LibraryWallpapers[i] == filtered[i]:  continue  // 已就位
    LibraryWallpapers.Insert(i, filtered[i])           // 在正确位置插入

  总 Insert 次数 = |filtered| - |Phase1后剩余|，不超线性
```

**为什么 Phase 2 无需 Move**：Phase 1 只删不移，保证了"有序子序列"不变式；
插入操作不会破坏后续位置已就位元素的相对顺序。

### 4.4 删除 + 过滤的顺序正确性
- 删除后 `_libraryWallpapers` 与 `_wallpaperIndexService` 同步重建
- `filtered` 始终来自最新的 `_libraryWallpapers`，自动反映删除结果
- Phase 2 按 `_libraryWallpapers` 顺序插入，最终顺序与 index service 一致

---

## 5. 类型过滤（`WpTypeFilterItem`）

用户可见的 4 个类别与 `FileType` 的映射：

| UI 标签 | 对应 `FileType` |
|---------|----------------|
| 静态图像 | `FImage` |
| 动态图像 | `FGif`, `FimageAI` |
| 视频 | `FVideo` |
| Web 交互式 | `FWebZip` |

`FDesign` / `FUnknown` 不参与类型过滤分组，在无过滤状态下正常展示。

---

## 6. 约束与注意事项

| 约束 | 说明 |
|------|------|
| `_libraryWallpapers` 过滤时只读 | 所有过滤操作只修改 `LibraryWallpapers`，不触碰 `_libraryWallpapers` |
| `UpdateLib` 使用全局 idx | 仅在无过滤或 idx 处于已加载范围时行为正确；过滤激活时调用 `UpdateLib` 会写错位置 |
| 增量加载与过滤并发 | `LoadMoreAsync` 直接 Add 到两个集合，不经过当前过滤逻辑；加载后的新项会绕过已激活的过滤器 |
| 引用相等假设 | Phase 2 用 `ReferenceEquals` 比较，依赖"两个集合共享同一批对象引用"的不变式 |
