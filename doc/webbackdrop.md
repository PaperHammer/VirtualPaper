# WebBackdrop 模块

`src/WebBackdrop` 是 WinUI 3 Web 壁纸创作模块，程序集根命名空间为 `Workloads.Creation.WebBackdrop`。它提供项目初始化、文件树、Monaco 文本编辑、多标签页、Markdown/图片预览、本地 Web 预览、属性编辑、问题面板、恢复和导出能力。

## 目录职责

| 目录/文件 | 职责 |
| --- | --- |
| `MainPage.xaml(.cs)` | 模块入口，实现创作运行时页面 |
| `Views/Components/WebEditor.*` | 编辑器编排、标签页、保存与预览交互 |
| `Views/Components/MonacoEditor.*` | WebView2 与 Monaco Editor 的桥接 |
| `Views/Components/EditorContent/` | 文本、Markdown、图片、欢迎页和兜底内容 |
| `Views/Tools/WebFileTreeControl.*` | 文件树 UI、拖放、右键菜单、行内重命名 |
| `ViewModels/WebFileTreeViewModel.cs` | 文件树状态、清单同步和文件操作 |
| `ViewModels/WebEditorViewModel.cs` | 已打开文件、选中项和编辑器状态 |
| `Core/Utils/WebProjectSession.cs` | 单个项目会话及资源生命周期 |
| `Core/Utils/ProjectFileManager.cs` | 文件监听、变更合并、manifest 联动 |
| `Core/Utils/LocalPreviewServer.cs` | 项目目录的本地预览服务 |
| `Models/SerializableData/WebDesignFileUtil.cs` | `.vpw` 清单与 `project.json` 元数据 |
| `Assets/templates/v1/` | 新 Web 项目模板 |
| `Assets/monaco.html` | Monaco 宿主页与原生消息桥 |

## 项目格式

Web 工程既可以由 `.vpw` 文件标识，也可以由项目目录标识。若传入目录，默认项目文件为 `{目录名}.vpw`。创建空项目时会复制 `Assets/templates/v1`，并保证项目文件和元数据存在。

`.vpw` 是版本化 JSON manifest，核心字段如下：

```json
{
  "version": 1,
  "name": "project-name",
  "files": [
    { "path": "project-name.vpw", "type": "vpw", "role": "solution" },
    { "path": "index.html", "type": "html", "role": "entry" },
    { "path": "project.json", "type": "json", "role": "metadata" }
  ]
}
```

`WebDesignFileUtil` 通过 role 查找入口和元数据，而不是只依赖固定文件名。manifest 内使用相对路径；路径比较忽略大小写。目录导入会递归登记，删除/重命名目录会同步其后代条目。写入采用临时文件再替换的原子策略。

## 会话与数据流

```text
MainPage / WebEditor
        │
        ▼
WebProjectSession
   ├─ WebDesignFileUtil ──► .vpw manifest / project.json
   ├─ ProjectFileManager ─► ProjectSystemManager / FileSystemWatcher
   └─ LocalPreviewServer ─► 浏览器/WebView 本地预览
```

创建会话时先确保项目结构，再启动本地预览服务和文件系统监听。释放会话时必须停止监听与预览服务，因此新增生命周期逻辑应挂接到 `Dispose`。

## 文件树

`WebFileTreeControl` 只处理 WinUI 交互并向外发出打开、保存、另存为和目录选择事件；磁盘及树状态操作集中在 `WebFileTreeViewModel`。

当前文件树支持：

- manifest 驱动的层级展示和增量同步；
- 200 ms 防抖的按名称过滤；
- 新建、导入、剪切、复制、粘贴、删除和资源管理器定位；
- 外部文件/目录拖入复制；
- TreeView 内部拖动移动或同级排序；
- VS Code 风格行内重命名，文件默认只选中扩展名前的名称；
- 磁盘缺失项和项目文件的特殊菜单与拖动限制；
- 通过 manifest 占位展示尚未存在于磁盘的项目项。

TreeView 会先处理拖放事件，因此控件构造函数以 `handledEventsToo=true` 注册外部 `DragOver` 和 `Drop`。修改拖放时要区分外部复制与内部移动，避免覆盖 TreeView 原生的 `AcceptedOperation`。

## 编辑与预览

文本文件交给 Monaco，Markdown 和图片使用专用预览，无法识别的格式进入 fallback 页面。编辑器资源由 `WebBackdrop.csproj` 复制到输出目录，并把带版本号的 Monaco 源目录映射为稳定的 `Assets/monaco-editor/` 目标路径。

本地预览服务以项目目录为根。入口文件优先取 manifest 中 `role=entry` 的条目，找不到时回退到项目元数据的 `file`，最后回退到 `index.html`。

## 变更与冲突处理

文件监听的修改/重载/冲突事件以路径为键合并，连续事件在 150 ms 安静期后统一处理。文档跟踪器记录打开文件和脏状态，使外部修改遇到未保存编辑时进入冲突流程。以下文件不会进入正常项目变更：

- 原子写入产生的 `.*.tmp`；
- 调试运行时生成的 `wp_metadata_basic.json`；
- 被“另存为”等流程显式标记为忽略的下一次 Created 事件。

## 修改建议与验证点

涉及文件树或保存链路的改动，至少人工验证：

1. 新建文件和目录，manifest 条目正确；
2. 外部拖入文件和含子目录的目录；
3. 内部跨目录移动、同级排序和选中项保持；
4. 行内重命名的 Enter、Escape、失焦、非法字符和同名处理；
5. 删除、剪切/复制/粘贴及项目文件保护；
6. 编辑后保存、另存为、关闭未保存标签；
7. 编辑器有脏数据时从外部修改或删除文件；
8. manifest 被外部编辑后文件树增量同步；
9. 预览入口、Markdown、图片以及未知格式；
10. 页面关闭后监听器和本地预览端口释放。

独立调试可从 `VirtualPaper.Sandbox.WinUI.Preview` 入手；最终仍应在 `VirtualPaper.UI` 的草稿工作区中验证依赖注入、项目加载和保存协调是否完整。
