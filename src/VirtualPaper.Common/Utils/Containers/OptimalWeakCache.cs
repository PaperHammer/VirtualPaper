namespace VirtualPaper.Common.Utils.Containers {
    public class StringKeyWeakCache<TValue> : IDisposable where TValue : class {
        private readonly Dictionary<string, (WeakReference reference, DateTime lastAccess)> _cache = [];
        private readonly object _lock = new();
        private readonly Timer _cleanupTimer;
        private int _accessCount = 0;

        public StringKeyWeakCache() {
            _cleanupTimer = new Timer(TimerCleanup, null,
                TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
        }

        public TValue? Get(string key) {
            if (string.IsNullOrEmpty(key)) return null;

            lock (_lock) {
                if (_cache.TryGetValue(key, out var item) &&
                    item.reference.Target is TValue value) {

                    _cache[key] = (item.reference, DateTime.Now);

                    if (++_accessCount % 20 == 0) {
                        QuickCleanup();
                    }

                    return value;
                }

                _cache.Remove(key);

                return null;
            }
        }

        public void Set(string key, TValue value) {
            if (string.IsNullOrEmpty(key)) return; // 字符串特定检查
            ArgumentNullException.ThrowIfNull(value);

            lock (_lock) {
                _cache[key] = (new WeakReference(value), DateTime.Now);

                if (_cache.Count > 50) {
                    QuickCleanup();
                }
            }
        }

        private void QuickCleanup() {
            var deadKeys = _cache
                .Where(kvp => !kvp.Value.reference.IsAlive)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in deadKeys) {
                _cache.Remove(key);
            }
        }

        private void TimerCleanup(object? state) {
            lock (_lock) {
                var now = DateTime.Now;
                var itemsToRemove = _cache
                    .Where(kvp => !kvp.Value.reference.IsAlive ||
                                now - kvp.Value.lastAccess > TimeSpan.FromMinutes(10))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in itemsToRemove) {
                    _cache.Remove(key);
                }
            }
        }

        public void Dispose() {
            _cleanupTimer?.Dispose();

            lock (_lock) {
                _cache.Clear();
            }

            GC.SuppressFinalize(this);
        }
    }
}
