using System.Collections.Concurrent;
using VirtualPaper.Common;

namespace VirtualPaper.Cores.AppUpdate {
    /// <summary>
    /// Per-plugin async gate for coordinating plugin startup with updates.
    /// All plugins start in a locked state. Core releases after check/update completes.
    /// Uses ManualResetEventSlim (state-based, not consumed) so multiple waiters work.
    /// </summary>
    public static class UpdateLock {
        private static readonly ConcurrentDictionary<PluginName, ManualResetEventSlim> _gates = new();

        private static ManualResetEventSlim GetGate(PluginName plugin) =>
            _gates.GetOrAdd(plugin, _ => new ManualResetEventSlim(false));

        /// <summary>
        /// Pre-create gates for all known plugins. All start locked (unset).
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
        public static Task WaitAsync(PluginName plugin, CancellationToken token = default) {
            var gate = GetGate(plugin);
            if (gate.IsSet) return Task.CompletedTask;
            return Task.Run(() => gate.Wait(token), token);
        }

        /// <summary>
        /// Release the lock for a plugin, allowing pending startups to proceed.
        /// </summary>
        public static void Release(PluginName plugin) {
            if (_gates.TryGetValue(plugin, out var gate)) {
                gate.Set();
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
                kv.Value.Set();
            }
        }

        /// <summary>
        /// Re-lock a plugin, blocking its startup until released again.
        /// </summary>
        public static void Lock(PluginName plugin) {
            if (_gates.TryGetValue(plugin, out var gate)) {
                gate.Reset();
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
            _gates.TryGetValue(plugin, out var gate) && !gate.IsSet;

        /// <summary>
        /// Async wait until all registered plugins are unlocked.
        /// Only waits on gates that are currently locked.
        /// </summary>
        public static async Task WaitAllAsync(CancellationToken token = default) {
            var lockedGates = _gates.Values.Where(g => !g.IsSet).ToList();
            if (lockedGates.Count == 0) return;
            await Task.WhenAll(lockedGates.Select(g => Task.Run(() => g.Wait(token), token)));
        }

        /// <summary>
        /// Check if any registered plugin is currently locked.
        /// </summary>
        public static bool IsAnyLocked =>
            _gates.Any(kv => !kv.Value.IsSet);
    }
}
