# Hot Update Mechanism

## Supported Plugins

| Plugin | Type | Loaded By | Hot-Updatable |
|--------|------|-----------|---------------|
| **UI** | Separate process | Self | ✅ |
| **PlayerWeb** | Separate process | Self | ✅ |
| **ScrSaver** | Separate process | Self | ✅ |
| **ML** | DLL + model files | UI process | ✅ (UI restart required) |
| **Shaders** | Data files (.cso) | PlayerWeb process | ✅ (PlayerWeb restart required) |

**Update behavior:**
- Restart-style update always stops and restarts UI
- If PlayerWeb is being updated, it's also stopped and restarted with UI
- ML and Shaders don't need explicit stop — they're loaded automatically when UI/PlayerWeb starts
- UI startup is blocked if UI, ML, or Shaders are being updated

## Update Type Detection

When a new release is found, determine update type:

```
Fetch release → Check if "update_manifest.json" exists in release assets
├── Exists → Download and parse manifest
│    ├── type = "restart" → Restart-style: download plugin files per manifest
│    ├── type = "install" → Installer-style: download setup exe (current flow)
│    └── Parse failure → Fallback to installer-style
└── Not exists → Installer-style (backward compatible, current behavior)
```

### update_manifest.json Format

```json
{
  "type": "restart",
  "app_build": "2606R23T1430",
  "min_app_build": "2606R01T1200",
  "plugins": {
    "UI": {
      "build": "2606R23T1430",
      "asset": "plugin_UI_2606R23T1430.zip",
      "sha256": "abc..."
    },
    "PlayerWeb": {
      "build": "2606R20T1000",
      "asset": "plugin_PlayerWeb_2606R20T1000.zip",
      "sha256": "def..."
    }
  },
  "removed_plugins": ["ScrSaver"]
}
```

- `type`: `"restart"` (hot-update) or `"install"` (installer update)
- `app_build`: current app build number (format: `YYMM` + `R` + `DD` + `T` + `HHMM`, e.g., `2606R23T1430`)
- `min_app_build`: minimum app build required for this hot-update to be compatible
- `plugins`: map of plugin name to update info
  - `build`: plugin build number (only changes when this plugin is updated)
  - `asset`: release asset filename to download
  - `sha256`: expected hash for verification
- `removed_plugins`: list of plugin names to be removed (optional)
  - Client deletes local `Plugins/{name}/` directory for each entry
  - Supports complete plugin removal via hot-update

### Plugin Zip Structure

Each plugin zip (`plugin_{name}_{build}.zip`) contains the plugin folder contents directly:
```
plugin_UI_2606R23T1430.zip
├── VirtualPaper.UI.exe
├── VirtualPaper.UI.dll
├── ... (all plugin files)
```

The zip-level SHA256 is stored in `update_manifest.json` for download verification.

### Client Update Flow (Parallel Execution)

```
Phase 1: Download (UI still running)
1. Download all plugin zips in parallel
   └── Verify each zip SHA256 against manifest (parallel)
2. Write update flag (status="pending") with file hashes

Phase 2: Execute (triggered by UI close or core start)
3. Verify downloaded files against hashes in flag
4. Lock plugins (prevent startup)
5. Stop plugins (always stops UI)
6. Backup current plugins
7. Update flag to "in_progress"
8. Extract and replace all plugins in parallel
9. Update app_build.json
10. Process removed_plugins
11. Update flag to "completed", cleanup
12. Restart UI
```

### Pending Update Mechanism

If the user cancels UI close (e.g., unsaved work), the update stays in "pending" state:
- Downloaded files remain in `pending_updates/`
- Flag file indicates pending state
- On next UI close or core start, `CheckAndRecoverAsync` detects pending update and executes it

**Trigger points:**
- `UIRunnerService.Proc_UI_Exited` — when UI process exits normally
- `App.xaml.cs` startup — `CheckAndRecoverAsync` called on core start

**Safety:**
- Files are verified against stored hashes before execution
- If files are missing or corrupted, pending update is cleaned up
- `UpdateLock` prevents re-entry during active update

## Version Management

Each component has an independent build number (format: `YYMM` + `R` + `DD` + `T` + `HHMM`):

| Component | Build Number Rule | Example |
|-----------|-------------------|---------|
| App | Changes on **any** update | `2606R23T1430` → `2606R24T1000` (any update) |
| UI Plugin | Changes only when **UI** is updated | `2606R23T1430` → stays `2606R23T1430` if not updated |
| PlayerWeb Plugin | Changes only when **PlayerWeb** is updated | `2606R20T1000` → stays `2606R20T1000` if not updated |

### Local Version Tracking

`app_build.json` stores all build numbers:
```json
{
  "app_build": "2606R23T1430",
  "plugins": {
    "UI": "2606R23T1430",
    "PlayerWeb": "2606R20T1000"
  }
}
```

**File locations:**
- **Installation directory** (`BaseDirectory/app_build.json`): source of truth. Generated at build time, updated by hot-update.
- **AppData directory** (`AppDataDir/app_build.json`): synced copy. Force-overwritten from installation directory on every main process startup.

**Flow:**
1. Build time → MSBuild generates `app_build.json` in output directory
2. Installer → includes file, installs to installation directory
3. Main process startup → `AppBuildService.Refresh()` reads from installation dir → force-overwrites to AppData
4. Hot-update → `RestartUpdateService` updates installation directory's `app_build.json`
5. UI reads from AppData (always up-to-date after sync)

### Update Decision Logic

1. Read manifest
2. Read local `app_build.json`
3. Process `removed_plugins` (if any):
   - Delete local `Plugins/{name}/` directory for each entry
   - Remove from local `app_build.json`
4. For each plugin in manifest:
   - Compare local plugin build vs manifest build
   - Different or missing → needs update
   - Same → skip
5. Only download and replace plugins that need update
6. After successful update, update `app_build.json`

## Storage Structure

```
AppDataDir/
├── pending_updates/
│   ├── update.flag          # Flag file with update metadata
│   ├── UI/                  # New files for Plugins/UI/
│   ├── PlayerWeb/           # New files for Plugins/PlayerWeb/
│   ├── ScrSaver/            # New files for Plugins/ScrSaver/
│   └── _backup/             # Backup of current plugins (created during update)
│       ├── UI/
│       ├── PlayerWeb/
│       └── ScrSaver/
```

## update.flag Format

```json
{
  "status": "pending",
  "plugins": {
    "UI": {
      "target": "Plugins/UI",
      "files": [
        { "name": "VirtualPaper.UI.exe", "sha256": "abc..." },
        { "name": "VirtualPaper.UI.dll", "sha256": "def..." }
      ]
    }
  }
}
```

Status values: `pending` | `in_progress` | `completed`

## Update Flow

### Download Phase

1. Download new files to `pending_updates/{plugin}/`
2. Calculate SHA256 for each downloaded file
3. Write `update.flag` with `status="pending"`, file list + hashes

### Execution Phase (UI closed, triggered by main process)

1. Read `update.flag`
2. **Backup** current `plugins/` → `pending_updates/_backup/{plugin}/` (first step)
3. Verify all downloaded files against SHA256
   - Any failure → delete `pending_updates/` (no restore needed, originals untouched)
   - All pass → continue
4. Write `update.flag` with `status="in_progress"`
5. For each plugin:
   - Clear target folder
   - Copy new files from `pending_updates/{plugin}/`
   - Verify copied files match SHA256
6. All successful:
   - Delete `pending_updates/` (including backups)
   - Start UI
7. Any failure:
   - Full rollback from `_backup/`
   - Delete `pending_updates/`
   - Start UI

### Crash Recovery (main process startup)

1. `pending_updates/` does not exist → normal startup
2. `update.flag` missing or corrupted → rollback from `_backup/`, delete `pending_updates/`
3. `status="pending"` → re-verify files, proceed with execution if pass, else delete
4. `status="in_progress"` → rollback from `_backup/`, delete `pending_updates/`
5. `status="completed"` → delete `pending_updates/`

## Key Principles

- **No intermediate state**: either full success or full rollback
- **Backup first**: before any modifications, backup current plugins
- **Verify twice**: verify downloaded files before replacement, verify copied files after
- **Always cleanup**: all paths (success, failure, crash) end with `pending_updates/` deleted
- **Granularity**: per-plugin folder (each plugin is a separate process)

## Fallback Cases

- `pending_updates/` directory missing → rollback if `_backup/` exists
- `update.flag` corrupted → rollback from `_backup/`
- Files manually deleted → rollback from `_backup/` if available
- Backup also missing → log error, start UI with current state (may be broken)

## Rollback Notification

When a restart-style update is rolled back, write a notification file. UI displays a global message on next startup.

### File Location

`AppDataDir/update_rollback_notice.json`

### Format

```json
{
  "rollback": true
}
```

### Flow

1. **On rollback** (any path): write `update_rollback_notice.json` before cleaning up `pending_updates/`
2. **UI startup**: check if `update_rollback_notice.json` exists
   - Exists → display global message, then **delete the file**
   - Not exists → normal startup

### Message (based on current language setting)

- **中文**: "更新发生错误，请重试或执行全量更新"
- **English**: "Update failed. Please retry or perform a full update."
