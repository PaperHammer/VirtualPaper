using System;
using System.Collections.Concurrent;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common.Utils.ThreadContext;
using Windows.System;
using Windows.UI.Core;

namespace VirtualPaper.UIComponent.Utils {
    public sealed class KeyboardSinglePressUtil {
        public static KeyboardSinglePressUtil Instance => _instance.Value;

        private readonly struct ShortcutInfo(
            Action action,
            bool isPressed,
            VirtualKeyModifiers modifiers,
            string description) {
            public Action Action { get; } = action;
            public bool IsPressed { get; } = isPressed;
            public VirtualKeyModifiers Modifiers { get; } = modifiers;
            public string Desc { get; } = description;

            public readonly ShortcutInfo WithPressed(bool isPressed) =>
                new(Action, isPressed, Modifiers, Desc);
        }

        private KeyboardSinglePressUtil() { }

        /// <summary>
        /// 添加键盘事件监听器（支持多个UI组件）
        /// </summary>
        /// <param name="element">要监听的UI元素</param>
        public void AddListener(FrameworkElement element) {
            if (element == null) return;

            int key = element.GetHashCode();

            // 原子操作，确保不重复添加
            _listeners.GetOrAdd(key, _ => {
                element.KeyDown += OnKeyDown;
                element.KeyUp += OnKeyUp;
                element.Unloaded += OnElementUnloaded;
                return element;
            });
        }

        /// <summary>
        /// 移除键盘事件监听器
        /// </summary>
        /// <param name="element">要移除的UI元素</param>
        public void RemoveListener(FrameworkElement element) {
            if (element == null) return;

            int key = element.GetHashCode();
            if (_listeners.TryRemove(key, out var removedElement)) {
                removedElement.DispatcherQueue.TryEnqueue(() => {
                    removedElement.KeyDown -= OnKeyDown;
                    removedElement.KeyUp -= OnKeyUp;
                    removedElement.Unloaded -= OnElementUnloaded;
                });
            }
        }

        /// <summary>
        /// 注册快捷键
        /// </summary>
        public void RegisterShortcut(
            Action action,
            VirtualKey key,
            VirtualKeyModifiers modifiers = VirtualKeyModifiers.None,            
            string description = "") {
            string shortcutId = GetShortcutId(key, modifiers);
            _shortcuts.TryAdd(shortcutId,
                new ShortcutInfo(action, false, modifiers, description));
        }

        /// <summary>
        /// 注销快捷键
        /// </summary>
        public void UnregisterShortcut(VirtualKey key, VirtualKeyModifiers modifiers) {
            string shortcutId = GetShortcutId(key, modifiers);
            _shortcuts.TryRemove(shortcutId, out _);
        }

        /// <summary>
        /// 清理所有资源和监听器
        /// </summary>
        public void Cleanup() {
            foreach (var kvp in _listeners) {
                var element = kvp.Value;
                element.DispatcherQueue.TryEnqueue(() => {
                    element.KeyDown -= OnKeyDown;
                    element.KeyUp -= OnKeyUp;
                    element.Unloaded -= OnElementUnloaded;
                });
            }
            _listeners.Clear();
            _shortcuts.Clear();
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e) {
            var modifiers = GetCurrentModifiers();
            string shortcutId = GetShortcutId(e.Key, modifiers);

            if (_shortcuts.TryGetValue(shortcutId, out var oldShortcut) &&
                !oldShortcut.IsPressed &&
                oldShortcut.Action != null) {
                var newShortcut = oldShortcut.WithPressed(true);

                if (_shortcuts.TryUpdate(shortcutId, newShortcut, oldShortcut)) {
                    CrossThreadInvoker.InvokeOnUIThread(oldShortcut.Action.Invoke);
                    e.Handled = true;
                }
            }        
        }

        private void OnKeyUp(object sender, KeyRoutedEventArgs e) {
            var modifiers = GetCurrentModifiers();
            string shortcutId = GetShortcutId(e.Key, modifiers);

            if (_shortcuts.TryGetValue(shortcutId, out var oldShortcut)) {
                _shortcuts.TryUpdate(shortcutId, oldShortcut.WithPressed(false), oldShortcut);
            }
        }
        private void OnElementUnloaded(object sender, RoutedEventArgs e) {
            if (sender is FrameworkElement element) {
                RemoveListener(element);
            }
        }

        private static string GetShortcutId(VirtualKey key, VirtualKeyModifiers modifiers) {
            return $"{(int)modifiers}-{(int)key}";
        }

        private static VirtualKeyModifiers GetCurrentModifiers() {
            var modifiers = VirtualKeyModifiers.None;
            if (IsKeyPressed(VirtualKey.Control)) modifiers |= VirtualKeyModifiers.Control;
            if (IsKeyPressed(VirtualKey.Shift)) modifiers |= VirtualKeyModifiers.Shift;
            if (IsKeyPressed(VirtualKey.Menu)) modifiers |= VirtualKeyModifiers.Menu;
            if (IsKeyPressed(VirtualKey.LeftWindows) || IsKeyPressed(VirtualKey.RightWindows))
                modifiers |= VirtualKeyModifiers.Windows;
            return modifiers;
        }

        private static bool IsKeyPressed(VirtualKey key) {
            var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
            return (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }

        private readonly ConcurrentDictionary<string, ShortcutInfo> _shortcuts = new();
        private readonly ConcurrentDictionary<int, FrameworkElement> _listeners = new();
        private static readonly Lazy<KeyboardSinglePressUtil> _instance =
            new(() => new KeyboardSinglePressUtil());
    }
}