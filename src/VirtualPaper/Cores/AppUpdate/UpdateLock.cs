namespace VirtualPaper.Cores.AppUpdate {
    /// <summary>
    /// Static lock state for restart-style updates.
    /// Tracks which plugins are being updated to block only those.
    /// </summary>
    public static class UpdateLock {
        private static readonly HashSet<string> _updatingPlugins = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new();

        public static bool IsUpdating {
            get {
                lock (_lock) return _updatingPlugins.Count > 0;
            }
        }

        public static void SetUpdatingPlugins(IEnumerable<string> pluginNames) {
            lock (_lock) {
                _updatingPlugins.Clear();
                foreach (var name in pluginNames) {
                    _updatingPlugins.Add(name);
                }
            }
        }

        public static void ClearUpdatingPlugins() {
            lock (_lock) {
                _updatingPlugins.Clear();
            }
        }

        public static bool IsPluginUpdating(string pluginName) {
            lock (_lock) return _updatingPlugins.Contains(pluginName);
        }
    }
}
