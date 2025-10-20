using System;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using Windows.UI.ViewManagement;

namespace VirtualPaper.UI.Utils {
    /// <summary>
    /// 静态主题管理器，用于在运行时热切换 UI 主题，并监听系统主题变化。
    /// </summary>
    public static class ThemeManager {
        /// <summary>
        /// 注册主UI的根元素，并开始监听系统主题变化。
        /// 应在主窗口/MainPage加载后调用。
        /// </summary>
        /// <param name="rootElement">例如 MainRootGrid 或 Frame。</param>
        public static void RegisterThemeRoot(FrameworkElement rootElement) {
            if (_themeRootReference != null && _themeRootReference.TryGetTarget(out _)) {
                return;
            }

            _themeRootReference = new WeakReference<FrameworkElement>(rootElement);
            InitializeSystemThemeListener();
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
            if (_themeRootReference == null || !_themeRootReference.TryGetTarget(out FrameworkElement? rootElement) || rootElement == null) {
                return;
            }

            _ = rootElement.DispatcherQueue.TryEnqueue(() => {
                if (_currentPreference == AppTheme.Auto) {
                    ApplyTheme(AppTheme.Auto);
                }
            });
        }

        /// <summary>
        /// 将主题应用到注册的根元素，实现热切换。
        /// </summary>
        public static void ApplyTheme(AppTheme theme) {
            _currentPreference = theme;
            if (_themeRootReference == null || !_themeRootReference.TryGetTarget(out FrameworkElement? rootElement) || rootElement == null) {
                return;
            }

            ElementTheme elementTheme = theme switch {
                AppTheme.Auto => ElementTheme.Default,
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };

            if (rootElement.RequestedTheme != elementTheme) {
                rootElement.RequestedTheme = elementTheme;
            }
        }

        /// <summary>
        /// 释放资源。在主窗口关闭时调用，以取消事件订阅。
        /// </summary>
        public static void Cleanup() {
            if (_uiSettings != null) {
                _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
                _uiSettings = null;
            }
            _themeRootReference = null;
        }

        private static WeakReference<FrameworkElement>? _themeRootReference;
        private static UISettings? _uiSettings;
        private static AppTheme _currentPreference = AppTheme.Auto;
    }
}
