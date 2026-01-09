using System;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.UIComponent.Utils.Extensions;
using Windows.UI.ViewManagement;

namespace VirtualPaper.UIComponent.Utils {
    /// <summary>
    /// 静态主题管理器，用于在运行时热切换 UI 主题，并监听系统主题变化。
    /// </summary>
    public static class ArcThemeUtil {
        private static EventHandler<AppTheme>? _appThemeChanged;
        public static event EventHandler<AppTheme> AppThemeChanged {
            add {
                _appThemeChanged -= value;
                _appThemeChanged += value;
            }
            remove {
                _appThemeChanged -= value;
            }
        }

        public static AppTheme MainWindowAppTheme { get; private set; } = AppTheme.Auto;
        public static AppSystemBackdrop MainWindowBackdrop { get; private set; } = AppSystemBackdrop.Default;

        static ArcThemeUtil() {
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
            // 防抖：如果系统主题短时间内频繁切换，则忽略多余触发
            if ((DateTime.Now - _lastThemeApplyTime) < _minThemeInterval)
                return;

            _lastThemeApplyTime = DateTime.Now;

            if (MainWindowAppTheme == AppTheme.Auto) {
                CrossThreadInvoker.InvokeOnUIThread(() => {
                    UpdateThemeGlobal(AppTheme.Auto);
                });
            }
        }

        internal static void UpdateThemeGlobal(AppTheme theme) {
            SetMainWindowAppTheme(theme);
            ArcWindowManager.UpdateAllWindowTheme();
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
                    _ => ArcWindowManager.MainWindowRootFe.ActualTheme == ElementTheme.Dark ? "Dark" : "Light"
                };
            }
            else {
                // 没有上下文时，用全局偏好
                themeKey = MainWindowAppTheme switch {
                    AppTheme.Dark or AppTheme.Light => MainWindowAppTheme.ToString(),
                    _ => ArcWindowManager.MainWindowRootFe.ActualTheme == ElementTheme.Dark ? "Dark" : "Light",
                };
            }

            if (TryGetResourceFromThemeDictionary(themeKey, key, out object? rawResource) ||
                Application.Current.Resources.TryGetValue(key, out rawResource)) {
                resource = rawResource;
                return resource != null;
            }

            return false;
        }

        private static bool TryGetResourceFromThemeDictionary(string themeKey, string resourceKey, out object? resource) {
            if (Application.Current.Resources.ThemeDictionaries.TryGetValue(themeKey, out var dictObj) &&
                dictObj is ResourceDictionary dict) {
                return dict.TryGetValue(resourceKey, out resource);
            }
            resource = null;
            return false;
        }

        public static void Cleanup() {
            if (_uiSettings != null) {
                _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;                
                _uiSettings = null;
            }
        }

        internal static void ApplyTheme(FrameworkElement fe) {
            var elementTheme = MainWindowAppTheme.ToElementTheme();
            if (fe.RequestedTheme != elementTheme) {
                fe.RequestedTheme = elementTheme;
            }
        }

        // 避免 Auto 导致 TitleBar 文案颜色不正确
        public static AppTheme GetFormatMainWindowTheme() {
            if (MainWindowAppTheme != AppTheme.Auto)
                return MainWindowAppTheme;

            return ArcWindowManager.MainWindowRootFe.ActualTheme.ToAppTheme();
        }

        internal static void SetMainWindowAppTheme(AppTheme appTheme) {
            MainWindowAppTheme = appTheme;
            _appThemeChanged?.Invoke(null, appTheme);
        }

        internal static void SetMainWindowBackdrop(AppSystemBackdrop systemBackdrop) {
            MainWindowBackdrop = systemBackdrop;
        }

        private static UISettings? _uiSettings;
        private static readonly TimeSpan _minThemeInterval = TimeSpan.FromMilliseconds(1000);
        private static DateTime _lastThemeApplyTime;
    }
}
