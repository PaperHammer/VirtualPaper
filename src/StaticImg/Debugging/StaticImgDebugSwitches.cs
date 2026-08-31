namespace Workloads.Creation.StaticImg.Debugging {
    /// <summary>
    /// StaticImg 本地渲染诊断开关。
    /// Debug 构建可修改默认值或在调试器即时窗口动态设置；Release 构建始终关闭。
    /// </summary>
    public static class StaticImgDebugSwitches {
#if DEBUG
        /// <summary>
        /// 显示稳定笔画动态缓存、当前提交范围、活动笔画范围和画布脏区。
        /// </summary>
        public static bool ShowStrokeCacheOverlay { get; set; } = false;
#else
        public static bool ShowStrokeCacheOverlay {
            get => false;
            set { }
        }
#endif
    }
}
