namespace VirtualPaper.Common.Utils.DI {
    public class GeneralFactory<T> where T : class, IDisposable {
        // 禁止外部实例化
        private GeneralFactory() { }

        /// <summary>
        /// 获取单例实例（线程安全）
        /// </summary>
        public static T Instance {
            get {
                if (_instance == null) {
                    lock (_lockObj) {
                        _instance ??= CreateInstance();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 自定义实例创建逻辑（可被重写）
        /// </summary>
        protected static T CreateInstance() {
            // 默认使用无参构造函数
            return Activator.CreateInstance<T>();
        }

        /// <summary>
        /// 释放单例资源
        /// </summary>
        public static void Release() {
            lock (_lockObj) {
                _instance?.Dispose();
                _instance = null;
            }
        }

        // 单例实例（volatile 确保可见性）
        private static volatile T _instance;
        private static readonly object _lockObj = new();
    }
}
