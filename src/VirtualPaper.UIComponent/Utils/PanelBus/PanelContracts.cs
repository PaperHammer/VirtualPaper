namespace VirtualPaper.UIComponent.Utils {
    /// <summary>
    /// 所有 Panel 在 <see cref="PanelMessageCenter"/> 中的信道标识、Action Key、Event Key 统一约定。
    /// <para>
    /// 规则：
    /// <list type="bullet">
    ///   <item>每个 Panel 对应一个嵌套静态类，类名即为该 Panel 的名称。</item>
    ///   <item><c>Id</c> 为信道标识，注册与调用时传入的 <c>panelId</c> 必须与此一致。</item>
    ///   <item><c>Action_*</c> 前缀表示请求-响应型能力（由该 Panel 注册，其他 Panel 调用）。</item>
    ///   <item><c>Event_*</c>  前缀表示发布-订阅型通知（由该 Panel 发布，其他 Panel 订阅）。</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class PanelContracts {
        // ─────────────────────────────────────────────────────────────────
        // WpSettings Panel
        // ─────────────────────────────────────────────────────────────────
        public static class WpSettings {
            /// <summary>信道 Id</summary>
            public const string Id = "WpSettings";

            /// <summary>
            /// Action：将本地文件路径导入壁纸库。
            /// 入参：<c>string filePath</c>，返回：<c>bool</c>（true = 成功）。
            /// </summary>
            public const string Action_ImportWallpaper = "ImportWallpaper";

            /// <summary>
            /// Action：预览本地文件（无需入库，直接打开预览窗口）。
            /// 入参：<c>string filePath</c>，返回：<c>bool</c>（true = 成功打开）。
            /// </summary>
            public const string Action_PreviewFile = "PreviewFile";

            /// <summary>
            /// Event：壁纸库新增条目后广播。
            /// 载荷：新增壁纸的 <c>FolderPath</c>（string）。
            /// </summary>
            public const string Event_WallpaperImported = "WallpaperImported";
        }
    }
}
