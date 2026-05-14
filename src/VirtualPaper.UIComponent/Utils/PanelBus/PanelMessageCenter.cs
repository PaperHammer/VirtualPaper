using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VirtualPaper.UIComponent.Utils {
    /// <summary>
    /// 跨 Panel 消息中心，按信道（Panel）隔离，支持两种通信模式：
    /// <list type="bullet">
    ///   <item><b>Action（请求-响应）</b>：一个 Panel 注册某项能力，其他 Panel 通过 InvokeAsync 调用，类似 RPC。</item>
    ///   <item><b>Event（发布-订阅）</b>：一个 Panel 发布事件，其他 Panel 订阅，类似消息队列。</item>
    /// </list>
    /// <para>
    /// 所有 Panel 的信道标识、Action Key、Event Key 统一定义在 <see cref="PanelContracts"/> 中。
    /// </para>
    /// </summary>
    public static partial class PanelMessageCenter {
        // ─────────────────────────────────────────────────────────────────
        // Action（请求-响应）
        // Key: (panelId, actionId)  Value: Func<object?, Task<object?>>
        // ─────────────────────────────────────────────────────────────────
        private static readonly ConcurrentDictionary<(string panelId, string actionId), Func<object?, Task<object?>>>
            _actions = new();

        // ─────────────────────────────────────────────────────────────────
        // Event（发布-订阅）
        // Key: (panelId, eventId)  Value: list of typed Action<object?>
        // ─────────────────────────────────────────────────────────────────
        private static readonly ConcurrentDictionary<(string panelId, string eventId), List<Action<object?>>>
            _events = new();

        private static readonly object _eventLock = new();

        // ═════════════════════════════════════════════════════════════════
        // Action API
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// 注册一项可被其他 Panel 调用的能力（Action）。
        /// 同一 (panelId, actionId) 重复注册时，以最新注册为准。
        /// </summary>
        /// <typeparam name="TArg">入参类型</typeparam>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="panelId">注册方的信道标识，建议使用 <see cref="PanelContracts"/> 中的常量</param>
        /// <param name="actionId">能力标识，建议使用 <see cref="PanelContracts"/> 中的常量</param>
        /// <param name="handler">实现逻辑</param>
        public static void RegisterAction<TArg, TResult>(
            string panelId,
            string actionId,
            Func<TArg, Task<TResult>> handler) {
            ArgumentNullException.ThrowIfNull(handler);

            _actions[(panelId, actionId)] = async arg => {
                var typedArg = arg is TArg t ? t : default!;
                return await handler(typedArg);
            };
        }

        /// <summary>
        /// 注销一项已注册的 Action。
        /// </summary>
        public static void UnregisterAction(string panelId, string actionId) {
            _actions.TryRemove((panelId, actionId), out _);
        }

        /// <summary>
        /// 调用另一个 Panel 已注册的 Action（跨 Panel RPC）。
        /// 若目标未注册则抛出 <see cref="InvalidOperationException"/>。
        /// </summary>
        public static async Task<TResult> InvokeAsync<TArg, TResult>(
            string panelId,
            string actionId,
            TArg arg) {
            if (!_actions.TryGetValue((panelId, actionId), out var handler)) {
                throw new InvalidOperationException(
                    $"[PanelMessageCenter] Action not registered: panel='{panelId}', action='{actionId}'");
            }

            var raw = await handler(arg);
            return raw is TResult result ? result : default!;
        }

        /// <summary>
        /// 尝试调用另一个 Panel 已注册的 Action。
        /// 若目标未注册，返回 (false, default) 而不抛异常。
        /// </summary>
        public static async Task<(bool found, TResult? result)> TryInvokeAsync<TArg, TResult>(
            string panelId,
            string actionId,
            TArg arg) {
            if (!_actions.TryGetValue((panelId, actionId), out var handler)) {
                return (false, default);
            }

            var raw = await handler(arg);
            return (true, raw is TResult result ? result : default);
        }

        // ═════════════════════════════════════════════════════════════════
        // Event API
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// 订阅指定 Panel 信道的事件。
        /// </summary>
        /// <typeparam name="TEvent">事件载荷类型</typeparam>
        /// <param name="panelId">目标 Panel 的信道标识</param>
        /// <param name="eventId">事件标识</param>
        /// <param name="handler">事件处理器</param>
        /// <returns>取消订阅令牌：对返回值调用 Dispose() 即可注销。</returns>
        public static IDisposable Subscribe<TEvent>(
            string panelId,
            string eventId,
            Action<TEvent> handler) {
            ArgumentNullException.ThrowIfNull(handler);

            Action<object?> wrapper = raw => {
                if (raw is TEvent e) handler(e);
            };

            lock (_eventLock) {
                var handlers = _events.GetOrAdd((panelId, eventId), _ => []);
                handlers.Add(wrapper);
            }

            return new EventSubscription(() => {
                lock (_eventLock) {
                    if (_events.TryGetValue((panelId, eventId), out var list)) {
                        list.Remove(wrapper);
                    }
                }
            });
        }

        /// <summary>
        /// 向指定信道发布事件，所有订阅者同步回调。
        /// 单个订阅者抛出异常不会影响其他订阅者。
        /// </summary>
        public static void Publish<TEvent>(
            string panelId,
            string eventId,
            TEvent payload) {
            List<Action<object?>>? snapshot;

            lock (_eventLock) {
                if (!_events.TryGetValue((panelId, eventId), out var handlers) || handlers.Count == 0)
                    return;

                snapshot = [.. handlers];
            }

            foreach (var handler in snapshot) {
                try {
                    handler(payload);
                }
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PanelMessageCenter] Subscriber threw: panel='{panelId}', event='{eventId}': {ex}");
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // 生命周期清理（Panel 卸载时调用）
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// 注销某个 Panel 信道注册的所有 Action。
        /// </summary>
        public static void UnregisterAllActions(string panelId) {
            foreach (var key in _actions.Keys) {
                if (key.panelId == panelId)
                    _actions.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// 移除某个 Panel 信道上的所有 Event 订阅者。
        /// </summary>
        public static void ClearAllSubscriptions(string panelId) {
            lock (_eventLock) {
                foreach (var key in _events.Keys) {
                    if (key.panelId == panelId)
                        _events[key].Clear();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 内部：取消订阅令牌
        // ─────────────────────────────────────────────────────────────────
        private sealed partial class EventSubscription(Action unsubscribe) : IDisposable {
            private bool _disposed;

            public void Dispose() {
                if (_disposed) return;
                _disposed = true;
                unsubscribe();
            }
        }
    }
}
