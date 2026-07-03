using System.Collections.Concurrent;
using VirtualPaper.Common;

namespace VirtualPaper.Cores.AppUpdate {
    /// <summary>
    /// Per-plugin async gate for coordinating plugin startup with updates.
    /// All plugins start in a locked state. Core releases after check/update completes.
    /// Reusable across multiple update cycles.
    /// </summary>
    public static class UpdateLock {
        private static readonly ConcurrentDictionary<PluginName, SemaphoreSlim> _gates = new();

        private static SemaphoreSlim GetGate(PluginName plugin) =>
            _gates.GetOrAdd(plugin, _ => new SemaphoreSlim(0, 1));

        /// <summary>
        /// Pre-create gates for all known plugins. All start locked (count=0).
        /// Call once at startup before any plugin attempts to start.
        /// </summary>
        public static void RegisterAll() {
            foreach (var plugin in Enum.GetValues<PluginName>()) {
                _ = GetGate(plugin);
            }
        }

        /// <summary>
        /// Async wait until the plugin is free to start. Blocks while locked.
        /// </summary>
        public static Task WaitAsync(PluginName plugin, CancellationToken token = default) =>
            GetGate(plugin).WaitAsync(token);

        /// <summary>
        /// Release the lock for a plugin, allowing pending startups to proceed.
        /// </summary>
        public static void Release(PluginName plugin) {
            if (_gates.TryGetValue(plugin, out var gate) && gate.CurrentCount == 0) {
                gate.Release();
            }
        }

        /// <summary>
        /// Release locks for multiple plugins at once.
        /// </summary>
        public static void ReleaseAll(IEnumerable<PluginName> plugins) {
            foreach (var plugin in plugins) Release(plugin);
        }

        /// <summary>
        /// Release all registered plugin locks.
        /// </summary>
        public static void ReleaseAll() {
            foreach (var kv in _gates) {
                if (kv.Value.CurrentCount == 0) {
                    kv.Value.Release();
                }
            }
        }

        /// <summary>
        /// Re-lock a plugin, blocking its startup until released again.
        /// Only works if the plugin is currently unlocked (count=1).
        /// </summary>
        public static void Lock(PluginName plugin) {
            if (_gates.TryGetValue(plugin, out var gate) && gate.CurrentCount > 0) {
                gate.Wait();
            }
        }

        /// <summary>
        /// Re-lock multiple plugins.
        /// </summary>
        public static void LockAll(IEnumerable<PluginName> plugins) {
            foreach (var plugin in plugins) Lock(plugin);
        }

        /// <summary>
        /// Check if a plugin is currently locked.
        /// </summary>
        public static bool IsLocked(PluginName plugin) =>
            _gates.TryGetValue(plugin, out var gate) && gate.CurrentCount == 0;

        /// <summary>
        /// Async wait until all registered plugins are unlocked.
        /// </summary>
        public static async Task WaitAllAsync(CancellationToken token = default) {
            var tasks = _gates.Values.Select(g => g.WaitAsync(token));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Check if any registered plugin is currently locked.
        /// </summary>
        public static bool IsAnyLocked =>
            _gates.Any(kv => kv.Value.CurrentCount == 0);
    }
}
