namespace VirtualPaper.Services.Interfaces {
    /// <summary>
    /// 外部服务，用于在发生故障时监控和关闭壁纸插件
    /// </summary>
    public interface IWatchdogService {
        public bool IsRunning { get; }

        /// <summary>
        /// 添加监视进程
        /// </summary>
        /// <param name="pid">进程 ID.</param>
        void Add(int pid);

        /// <summary>
        /// 清除当前正在监视的所有进程
        /// </summary>
        void Clear();

        /// <summary>
        /// 从监视列表中移除目标进程
        /// </summary>
        /// <param name="pid">进程 ID</param>
        void Remove(int pid);

        /// <summary>
        /// 开始监视
        /// </summary>
        void Start();
    }
}
