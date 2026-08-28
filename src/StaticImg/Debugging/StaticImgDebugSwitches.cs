namespace Workloads.Creation.StaticImg.Debugging {
    /// <summary>
    /// StaticImg 本地渲染诊断开关。
    /// Debug 构建可修改默认值或在调试器即时窗口动态设置；Release 构建始终关闭。
    /// </summary>
    public static class StaticImgDebugSwitches {
#if DEBUG
        /// <summary>
        /// 显示笔画瓦片、当前帧更新瓦片、活动笔画范围和画布脏区。
        /// </summary>
        public static bool ShowStrokeTileOverlay { get; set; } = false;
#else
        public static bool ShowStrokeTileOverlay {
            get => false;
            set { }
        }
#endif
    }
}
