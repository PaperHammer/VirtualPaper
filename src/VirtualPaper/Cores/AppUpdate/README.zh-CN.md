# 热更新机制

[English](README.md) | [简体中文](README.zh-CN.md)

> **历史设计：** 下文介绍早期的 `update_manifest.json` / `app_build.json` 设计，并非当前更新协议。当前发布使用 `app_comp_manifest.json`，插件补丁中包含 `pending_update_plugins_manifest.json`。当前实现请从 [AppBuildService.cs](AppBuildService.cs)、[PluginsUpdateService.cs](Specific/PluginsUpdateService.cs) 和[工作流指南](../../../../.github/workflows/README.zh-CN.md) 阅读。工作流指南说明当前 `YYMMRxx` 构建号规则；下方示例保留旧格式。

## 支持的插件

| 插件 | 类型 | 加载方 | 支持热更新 |
| --- | --- | --- | --- |
| **UI** | 独立进程 | 自身 | ✅ |
| **PlayerWeb** | 独立进程 | 自身 | ✅ |
| **ScrSaver** | 独立进程 | 自身 | ✅ |
| **ML** | DLL 与模型文件 | UI 进程 | ✅（需要重启 UI） |
| **Shaders** | 数据文件（.cso） | PlayerWeb 进程 | ✅（需要重启 PlayerWeb） |

**更新行为：**

- 重启式更新始终停止并重启 UI。
- 如果更新 PlayerWeb，也会随 UI 一起停止并重启 PlayerWeb。
- ML 和 Shaders 无需显式停止，UI/PlayerWeb 启动时会自动加载。
- 更新 UI、ML 或 Shaders 期间，阻止 UI 启动。

## 更新类型检测

发现新发布后，判断更新类型：

```text
获取发布 → 检查发布附件中是否存在 "update_manifest.json"
├── 存在 → 下载并解析清单
│    ├── type = "restart" → 重启式：根据清单下载插件文件
│    ├── type = "install" → 安装式：下载安装程序（现有流程）
│    └── 解析失败 → 回退到安装式更新
└── 不存在 → 安装式更新（向后兼容，保持现有行为）
```

### update_manifest.json 格式

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

- `type`：`"restart"`（热更新）或 `"install"`（安装包更新）。
- `app_build`：当前应用构建号，格式为 `YYMM` + `R` + `DD` + `T` + `HHMM`，例如 `2606R23T1430`。
- `min_app_build`：兼容本次热更新所需的最低应用构建号。
- `plugins`：插件名称到更新信息的映射。
  - `build`：插件构建号，仅在该插件更新时变化。
  - `asset`：需要下载的发布附件文件名。
  - `sha256`：用于校验的预期哈希。
- `removed_plugins`：需要移除的插件名称列表，可选。
  - 客户端删除每个条目对应的本地 `Plugins/{name}/` 目录。
  - 支持通过热更新完整移除插件。

### 插件 ZIP 结构

每个插件压缩包（`plugin_{name}_{build}.zip`）直接包含插件目录中的文件：

```text
plugin_UI_2606R23T1430.zip
├── VirtualPaper.UI.exe
├── VirtualPaper.UI.dll
├── ...（全部插件文件）
```

压缩包整体的 SHA256 保存在 `update_manifest.json` 中，用于下载校验。

### 客户端更新流程（并行执行）

```text
阶段 1：下载（UI 仍在运行）
1. 并行下载全部插件压缩包
   └── 并行核验每个压缩包的 SHA256 是否与清单一致
2. 写入更新标记（status="pending"），包含文件哈希

阶段 2：执行（由 UI 关闭或 Core 启动触发）
3. 根据标记中的哈希校验已下载文件
4. 锁定插件，阻止启动
5. 停止插件，始终停止 UI
6. 备份当前插件
7. 将标记更新为 "in_progress"
8. 并行解压、替换所有插件
9. 更新 app_build.json
10. 处理 removed_plugins
11. 将标记更新为 "completed" 并清理
12. 重启 UI
```

### 待执行更新机制

如果用户取消关闭 UI，例如尚有未保存的工作，更新保持在 `pending` 状态：

- 已下载文件保留在 `pending_plugins_update/` 中。
- 标记文件记录待执行状态。
- 下次 UI 关闭或 Core 启动时，`CheckAndRecoverAsync` 检测并执行待更新内容。

**触发点：**

- `UIRunnerService.Proc_UI_Exited`：UI 进程正常退出时。
- `App.xaml.cs` 启动流程：Core 启动时调用 `CheckAndRecoverAsync`。

**安全措施：**

- 执行前根据已存储哈希校验文件。
- 文件缺失或损坏时，清理待执行更新。
- `UpdateLock` 防止更新过程中重入。

## 版本管理

每个组件拥有独立构建号，格式为 `YYMM` + `R` + `DD` + `T` + `HHMM`：

| 组件 | 构建号规则 | 示例 |
| --- | --- | --- |
| App | **任何**更新都会变化 | `2606R23T1430` → `2606R24T1000`（任意更新） |
| UI 插件 | 仅 **UI** 更新时变化 | 未更新时，`2606R23T1430` 保持为 `2606R23T1430` |
| PlayerWeb 插件 | 仅 **PlayerWeb** 更新时变化 | 未更新时，`2606R20T1000` 保持为 `2606R20T1000` |

### 本地版本记录

`app_build.json` 保存全部构建号：

```json
{
  "app_build": "2606R23T1430",
  "plugins": {
    "UI": "2606R23T1430",
    "PlayerWeb": "2606R20T1000"
  }
}
```

**文件位置：**

- **安装目录**（`BaseDirectory/app_build.json`）：权威版本来源，构建时生成，热更新时更新。
- **AppData 目录**（`AppDataDir/app_build.json`）：同步副本，每次主进程启动时从安装目录强制覆盖。

**流程：**

1. 构建时：MSBuild 在输出目录生成 `app_build.json`。
2. 安装器：包含该文件，并安装到安装目录。
3. 主进程启动：`AppBuildService.Refresh()` 从安装目录读取，强制覆盖到 AppData。
4. 热更新：`PluginsUpdateService` 更新安装目录中的 `app_build.json`。
5. UI 从 AppData 读取，同步后保持最新。

### 更新决策逻辑

1. 读取清单。
2. 读取本地 `app_build.json`。
3. 若有 `removed_plugins`：
   - 删除每个条目对应的本地 `Plugins/{name}/` 目录。
   - 从本地 `app_build.json` 中移除。
4. 遍历清单中的每个插件：
   - 比较本地插件构建号与清单构建号。
   - 不同或缺失：需要更新。
   - 相同：跳过。
5. 仅下载、替换需要更新的插件。
6. 更新成功后写入 `app_build.json`。

## 存储结构

```text
AppDataDir/
├── pending_plugins_update/
│   ├── update.flag          # 带更新元数据的标记文件
│   ├── UI/                  # Plugins/UI/ 的新文件
│   ├── PlayerWeb/           # Plugins/PlayerWeb/ 的新文件
│   ├── ScrSaver/            # Plugins/ScrSaver/ 的新文件
│   └── _backup/             # 当前插件备份，更新时创建
│       ├── UI/
│       ├── PlayerWeb/
│       └── ScrSaver/
```

## update.flag 格式

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

状态值：`pending` | `in_progress` | `completed`

## 更新流程

### 下载阶段

1. 将新文件下载到 `pending_plugins_update/{plugin}/`。
2. 计算每个下载文件的 SHA256。
3. 写入 `update.flag`，包含 `status="pending"`、文件列表和哈希。

### 执行阶段（UI 已关闭，由主进程触发）

1. 读取 `update.flag`。
2. 首先将当前 `plugins/` **备份**到 `pending_plugins_update/_backup/{plugin}/`。
3. 校验所有已下载文件的 SHA256。
   - 任一失败：删除 `pending_plugins_update/`，原文件未被修改，无需恢复。
   - 全部通过：继续。
4. 写入 `status="in_progress"` 的 `update.flag`。
5. 对每个插件：
   - 清空目标文件夹。
   - 从 `pending_plugins_update/{plugin}/` 复制新文件。
   - 校验复制后文件的 SHA256。
6. 全部成功：
   - 删除 `pending_plugins_update/`，包括备份。
   - 启动 UI。
7. 任一失败：
   - 从 `_backup/` 完整回滚。
   - 删除 `pending_plugins_update/`。
   - 启动 UI。

### 崩溃恢复（主进程启动时）

1. `pending_plugins_update/` 不存在：正常启动。
2. `update.flag` 缺失或损坏：从 `_backup/` 回滚，删除 `pending_plugins_update/`。
3. `status="pending"`：重新校验文件，通过则执行更新，否则删除。
4. `status="in_progress"`：从 `_backup/` 回滚，删除 `pending_plugins_update/`。
5. `status="completed"`：删除 `pending_plugins_update/`。

## 关键原则

- **不保留中间状态**：要么全部成功，要么完整回滚。
- **先备份**：任何修改之前，先备份当前插件。
- **双重校验**：替换前校验下载文件，替换后校验复制文件。
- **始终清理**：成功、失败、崩溃路径最终都删除 `pending_plugins_update/`。
- **粒度**：以插件文件夹为单位，每个插件是一个独立进程。

## 兜底情况

- `pending_plugins_update/` 目录缺失：若存在 `_backup/`，则回滚。
- `update.flag` 损坏：从 `_backup/` 回滚。
- 文件被手动删除：若有 `_backup/`，则回滚。
- 备份也缺失：记录错误，以当前状态启动 UI，应用可能无法正常工作。

## 回滚通知

重启式更新回滚时写入通知文件，下次 UI 启动时显示全局消息。

### 文件位置

`AppDataDir/update_rollback_notice.json`

### 格式

```json
{
  "rollback": true
}
```

### 流程

1. **发生回滚时**，无论哪条路径，都在清理 `pending_plugins_update/` 之前写入 `update_rollback_notice.json`。
2. **UI 启动时**，检查 `update_rollback_notice.json` 是否存在。
   - 存在：显示全局消息，然后**删除文件**。
   - 不存在：正常启动。

### 消息（根据当前语言设置）

- **中文**："更新发生错误，请重试或执行全量更新"
- **English**："Update failed. Please retry or perform a full update."
