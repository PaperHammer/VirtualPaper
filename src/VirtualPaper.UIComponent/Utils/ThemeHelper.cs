using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.ThreadContext;
using Windows.UI.ViewManagement;

namespace VirtualPaper.UIComponent.Utils {
    /// <summary>
    /// 静态主题管理器，用于在运行时热切换 UI 主题，并监听系统主题变化。
    /// </summary>
    public static class ThemeHelper {
        public static event EventHandler? OnAppThemeChanged;

        public static AppTheme CurrentPreference { get; private set; } = AppTheme.Auto;

        /// <summary>
        /// 注册主UI的根元素，并开始监听系统主题变化。
        /// 应在主窗口/MainPage加载后调用。
        /// </summary>
        /// <param name="rootElement">例如 MainRootGrid 或 Frame。</param>
        public static void RegisterThemeRoot(FrameworkElement rootElement) {
            lock (_lock) {
                if (_themeRootReferences.Any(r => r.TryGetTarget(out var e) && e == rootElement))
                    return;

                _themeRootReferences.Add(new WeakReference<FrameworkElement>(rootElement));
                InitializeSystemThemeListener();
            }
        }

        /// <summary>
        /// 初始化 UISettings 监听器。
        /// </summary>
        private static void InitializeSystemThemeListener() {
            if (_uiSettings == null) {
                _uiSettings = new UISettings();
                _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
            }
        }

        /// <summary>
        /// 响应系统主题/颜色值变化的事件处理程序。
        /// </summary>
        private static void OnSystemColorValuesChanged(UISettings sender, object args) {
            if (_themeRootReferences == null || _themeRootReferences.Count == 0) {
                return;
            }

            // 防抖：如果系统主题短时间内频繁切换，则忽略多余触发
            if ((DateTime.Now - _lastThemeApplyTime) < _minThemeInterval)
                return;

            _lastThemeApplyTime = DateTime.Now;

            if (CurrentPreference == AppTheme.Auto) {
                CrossThreadInvoker.InvokeOnUIThread(() => {
                    WindowHelper.UpdateThemeFromSys(AppTheme.Auto);
                });
            }
        }

        /// <summary>
        /// 将主题应用到注册的根元素，实现热切换。
        /// </summary>
        public static void ApplyTheme(AppTheme theme) {
            // 避免并发 ApplyTheme（用户快速点击切换按钮 + 系统触发）
            // 防止并发重入（多线程或短时间重复调用 ApplyTheme）
            if (Interlocked.Exchange(ref _isApplying, 1) == 1)
                return; // 正在切换中

            try {
                // 保证内部共享资源一致性（对 _themeRootReferences 的并发访问）
                lock (_lock) {
                    CurrentPreference = theme;
                    ElementTheme elementTheme = theme switch {
                        AppTheme.Auto => ElementTheme.Default,
                        AppTheme.Light => ElementTheme.Light,
                        AppTheme.Dark => ElementTheme.Dark,
                        _ => ElementTheme.Default,
                    };

                    foreach (var weakRef in _themeRootReferences.ToArray()) {
                        if (weakRef.TryGetTarget(out var rootElement) && rootElement != null) {
                            if (rootElement.RequestedTheme != elementTheme) {
                                rootElement.RequestedTheme = elementTheme;
                            }
                        }
                        else {
                            _themeRootReferences.Remove(weakRef);
                        }
                    }

                    OnAppThemeChanged?.Invoke(null, EventArgs.Empty);
                }
            }
            finally {
                Interlocked.Exchange(ref _isApplying, 0);
            }
        }

        public static ElementTheme GetCurrentTheme(Window window) {
            return (window.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default;
        }

        public static bool TryGetThemeResource(string key, FrameworkElement? context, out object? resource) {
            resource = null;
            if (string.IsNullOrEmpty(key))
                return false;

            string themeKey;
            if (context is not null) {
                // 优先使用控件自己的主题
                var actualTheme = context.ActualTheme;
                themeKey = actualTheme switch {
                    ElementTheme.Dark or ElementTheme.Light => actualTheme.ToString(),
                    _ => Application.Current.RequestedTheme == ApplicationTheme.Dark ? "Dark" : "Light"
                };
            }
            else {
                // 没有上下文时，用全局偏好
                themeKey = CurrentPreference switch {
                    AppTheme.Dark or AppTheme.Light => CurrentPreference.ToString(),
                    _ => Application.Current.RequestedTheme == ApplicationTheme.Dark ? "Dark" : "Light",
                };
            }

            if (TryGetResourceFromThemeDictionary(themeKey, key, out object rawResource) ||
                Application.Current.Resources.TryGetValue(key, out rawResource)) {
                resource = rawResource;
                return resource != null;
            }

            return false;
        }

        private static bool TryGetResourceFromThemeDictionary(string themeKey, string resourceKey, out object resource) {
            if (Application.Current.Resources.ThemeDictionaries.TryGetValue(themeKey, out var dictObj) &&
                dictObj is ResourceDictionary dict) {
                return dict.TryGetValue(resourceKey, out resource);
            }
            resource = null;
            return false;
        }

        public static void Cleanup() {
            lock (_lock) {
                if (_uiSettings != null) {
                    _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
                    _uiSettings = null;
                }
                OnAppThemeChanged = null;
                _themeRootReferences.RemoveAll(r => !r.TryGetTarget(out _));
            }
        }

        private static readonly List<WeakReference<FrameworkElement>> _themeRootReferences = [];
        private static UISettings? _uiSettings;
        private static readonly object _lock = new();
        private static readonly TimeSpan _minThemeInterval = TimeSpan.FromMilliseconds(300);
        private static DateTime _lastThemeApplyTime;
        private static int _isApplying = 0;
    }
}
