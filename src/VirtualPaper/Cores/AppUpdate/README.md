# Hot Update Mechanism

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
  "app_build": "20260623.1",
  "min_app_build": "20260601.1",
  "plugins": {
    "UI": {
      "build": "20260623.1",
      "asset": "plugin_UI_20260623.1.zip",
      "sha256": "abc..."
    },
    "PlayerWeb": {
      "build": "20260620.1",
      "asset": "plugin_PlayerWeb_20260620.1.zip",
      "sha256": "def..."
    }
  }
}
```

- `type`: `"restart"` (hot-update) or `"install"` (installer update)
- `app_build`: current app build number
- `min_app_build`: minimum app build required for this hot-update to be compatible
- `plugins`: map of plugin name to update info
  - `build`: plugin build number (only changes when this plugin is updated)
  - `asset`: release asset filename to download
  - `sha256`: expected hash for verification

## Version Management

Each component has an independent build number:

| Component | Build Number Rule | Example |
|-----------|-------------------|---------|
| App | Changes on **any** update | `20260623.1` → `20260624.1` (any update) |
| UI Plugin | Changes only when **UI** is updated | `20260623.1` → stays `20260623.1` if not updated |
| PlayerWeb Plugin | Changes only when **PlayerWeb** is updated | `20260620.1` → stays `20260620.1` if not updated |

### Local Version Tracking

Each plugin folder contains `version.json`:
```json
{
  "build": "20260623.1"
}
```

### Update Decision Logic

1. Read manifest
2. For each plugin in manifest:
   - Read local `Plugins/{plugin}/version.json`
   - Compare `build` number
   - Different or missing → needs update
   - Same → skip
3. Only download and replace plugins that need update

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
