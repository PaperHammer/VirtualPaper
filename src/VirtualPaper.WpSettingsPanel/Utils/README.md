# WpSettingsPanel indexing and filtering

[English](README.md) | [简体中文](README.zh-CN.md)

## Data flow

```text
Wallpaper directories on disk
    │  Parallel startup scan; sort by CreateTime descending
    ▼
WallpaperIndexFile.Items               Full metadata list
    │
    ▼
WallpaperIndexService                  Runtime index
  _index.Items                         Metadata entries
  _uidToIndex: Dictionary<UID, int>     UID → position in Items
    │  Query(offset, limit)
    ▼
_libraryWallpapers                     Loaded, unfiltered objects
    │  Filtering changes only the visible collection
    ▼
LibraryWallpapers                      ObservableCollection shown in the UI
```

The initial scan orders entries newest first. Pagination loads full wallpaper objects only when needed. Filtering operates on the loaded objects, not every metadata entry on disk.

## Core structures

### WallpaperIndexEntry

A lightweight metadata snapshot for ordering and searching. It does not hold a complete `WpBasicData` object.

| Field | Purpose |
| --- | --- |
| `Uid` | Unique wallpaper identifier used as the mapping key |
| `FolderPath` | Wallpaper directory metadata |
| `JsonPath` | Path to `WpBasicData.json`, read when loading a page |
| `CreateTime` | Initial descending sort order |
| `Title` / `Author` / `Tags` | Search metadata; only Title currently participates in filtering |

### WallpaperIndexService

- `_index.Items` stores metadata entries in index order.
- `_uidToIndex` maps a UID to its global index for O(1) dictionary lookup.
- Removing entries or appending new entries rebuilds the position map with `RebuildUidIndexMapUnsafe()`.

## Lifecycle

### Initialization

```text
WallpaperIndexService.Initialize(dirs)
  └─ WallpaperIndexFile.RebuildIndexAsync(dirs)
       Scan wallpaper directories in parallel
       Read WpBasicData.json and create WallpaperIndexEntry snapshots
       Sort Items by CreateTime descending
  └─ RebuildUidIndexMapUnsafe()
  └─ Initialized.TrySetResult(true)
```

### Incremental loading

```text
LibraryContentsViewModel.InitContentAsync() / LoadMoreAsync()
  └─ _wallpaperIndexService.Query(_offset, _limit)
       Items.Skip(offset).Take(limit)
       Read the full WpBasicData for each entry
       LibraryWallpapers.Add(data)
       _libraryWallpapers.Add(data)   // The same object reference
       _offset++
```

The positional assumption is that `_libraryWallpapers[i]` corresponds to `_index.Items[i]`. Initial pagination establishes this alignment. Updates must preserve it; see the limitations below.

### Adding and updating: UpdateLib

The current ViewModel implementation follows this shape:

```csharp
if (_wallpaperIndexService.TryGetValue(data.WallpaperUid, out int idx)) {
    LibraryWallpapers[idx] = data;
    _libraryWallpapers[idx] = data;
}
else {
    // Put newly imported items at the front of both loaded collections.
    LibraryWallpapers.Insert(0, data);
    _libraryWallpapers.Insert(0, data);
}
_wallpaperIndexService.Update(data);
```

The service replaces an existing metadata entry in place, but appends a new one to `_index.Items`. Consequently, insertion at the front of the loaded collections does not itself preserve alignment with the index. `UpdateLib` also publishes the wallpaper-imported panel event for consumers such as previews and counters.

### Deleting: HandleDelete

```text
LibraryWallpapers.Remove(data)       Remove from the visible collection
_libraryWallpapers.Remove(data)      Remove from loaded objects
_wallpaperIndexService.Remove(data)  Remove metadata and rebuild the UID map
```

Rebuilding the map updates global positions after removal. Filtering then derives its results from the remaining loaded objects.

## Filtering: IFilterable and FilterContext

### Interface

```text
IFilterable
  FilterKey FilterKeyword          Identifies the consumer's filter dimension
  ApplyFilter(string keyword)      Compatibility entry point: title only
  ApplyFilter(FilterContext ctx)   Combined filter conditions
```

`FilterContext` is an immutable record carrying the active conditions. Its relevant fields are:

```text
TitleKeyword: string                  Empty means no title filter
ActiveTypes: IReadOnlySet<FileType>?   null means all types
```

New conditions can be added to the context without changing the interface signatures. Consumers read the fields they support.

### Broadcasting

```text
Search box / type selection
    ▼
WpSettingsViewModel.BroadcastFilter()
  Create FilterContext { TitleKeyword, ActiveTypes }
    ├─► LibraryContentsViewModel.ApplyFilter(ctx)
    └─► Other registered IFilterable consumers
```

Consumers are registered as `WeakReference<IFilterable>` so the subscription does not prevent garbage collection.

### Ordered synchronization

`ApplyFilter` selects an ordered subset of `_libraryWallpapers` by title and active file types. LINQ `Where` preserves the loaded list's order.

1. Remove nonmatching visible items from the end toward the beginning. Remaining entries keep their relative order and form an ordered subsequence of the filtered result.
2. Walk the filtered result from the beginning. If the visible item at that position is already the same object, continue; otherwise insert the missing object at that position.

Phase 2 needs no Move operation because phase 1 preserves the order of retained objects. The number of insertions equals the filtered count minus the retained count. This bounds the number of insertions, not the total running time: membership checks and collection shifts have their own costs.

After deletion, the filtered result comes from the latest `_libraryWallpapers`, so it reflects removed objects. The final visible order follows that loaded list; agreement with the metadata index depends on the positional assumption above.

## Type groups

`WpTypeFilterItem` exposes four groups:

| UI category | FileType values |
| --- | --- |
| Static images | `FImage` |
| Animated images | `FGif`, `FImageAI` |
| Video | `FVideo` |
| Interactive Web | `FWebZip` |

`FDesign`, `FWebDesign`, and `FUnknown` have no group and appear when no type filter is active.

## Current constraints

| Constraint | Implication |
| --- | --- |
| Filtering treats `_libraryWallpapers` as read-only | Only `LibraryWallpapers` is changed by filtering. |
| `UpdateLib` uses a global index for both loaded collections | The index must be loaded and the visible collection must have matching positions. An active filter can make the visible index incorrect. |
| New-item ordering differs between the ViewModel and index service | Loaded collections insert at the front; service metadata appends. Global positions can diverge after an import. |
| Incremental loading adds directly to both collections | Newly loaded items bypass an already active filter until filtering is reapplied. |
| Reinsertion uses ReferenceEquals | Both collections must share the same wallpaper object instances. |
