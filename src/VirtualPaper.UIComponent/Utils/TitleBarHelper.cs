using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Windowing;
using Windows.UI;

namespace VirtualPaper.UIComponent.Utils {
    public static class TitleBarHelper {
        public static void UpdateTitleBar(
            ArcWindow window,
            IReadOnlyList<FrameworkElement> titlebarElements,
            AppTheme currentTheme,
            bool isWindowActive) {
            UpdateSystemCaptionButtons(window, currentTheme, isWindowActive);
            UpdateTitleBarChildren(titlebarElements, currentTheme, isWindowActive);
        }

        public static void UpdateTitleBarVisualStates(
            Grid appTitleBar,
            AppTheme theme,
            AppSystemBackdrop backdrop) {
            appTitleBar.Background = GetOrCreateBackdropBrush(theme, backdrop);
        }

        public static void UpdateNaviVisualStates(
            NavigationView navView,
            AppTheme theme,
            AppSystemBackdrop backdrop) {
            navView.Background = GetOrCreateBackdropBrush(theme, backdrop);
        }

        private static void UpdateSystemCaptionButtons(Window window, AppTheme theme, bool isActive) {
            if (window.AppWindow?.TitleBar == null) return;

            var foreground = GetCachedSolidBrush(
                isActive
                    ? (theme == AppTheme.Dark ? _activeDarkForeground : _activeLightForeground)
                    : _inactiveForeground
            );

            window.AppWindow.TitleBar.ButtonForegroundColor = foreground.Color;
            window.AppWindow.TitleBar.ButtonHoverForegroundColor = foreground.Color;

            window.AppWindow.TitleBar.ButtonHoverBackgroundColor = theme == AppTheme.Dark
                ? _hoverDarkBackground
                : _hoverLightBackground;
        }

        private static void UpdateTitleBarChildren(
            IReadOnlyList<FrameworkElement> titlebarElements,
            AppTheme theme,
            bool isActive) {
            if (titlebarElements == null) return;

            var activeBrush = GetCachedSolidBrush(
                theme == AppTheme.Dark ? _activeDarkForeground : _activeLightForeground
            );
            var inactiveBrush = GetCachedSolidBrush(_inactiveForeground);
            var subtitleBrush = GetCachedSolidBrush(
                theme == AppTheme.Dark ? _subtitleDarkColor : _subtitleLightColor
            );

            foreach (var element in titlebarElements) {
                switch (element) {
                    case TextBlock textBlock:
                        bool isMainTitle = element.Tag?.ToString() == "main";
                        textBlock.Foreground = isActive
                            ? (isMainTitle ? activeBrush : subtitleBrush)
                            : inactiveBrush;
                        break;

                    case ImageIcon imageIcon:
                        imageIcon.Foreground = isActive ? activeBrush : inactiveBrush;
                        break;
                }
            }
        }

        #region cache
        private static SolidColorBrush GetCachedSolidBrush(Color color) {
            return _solidBrushCache.GetOrAdd(color, c => new SolidColorBrush(c));
        }

        private static Brush GetOrCreateBackdropBrush(AppTheme theme, AppSystemBackdrop backdrop) {
            return backdrop switch {
                AppSystemBackdrop.Mica => theme == AppTheme.Light
                    ? GetCachedSolidBrush(Color.FromArgb(242, 242, 242, 242)) // Light Mica
                    : GetCachedSolidBrush(Color.FromArgb(230, 32, 32, 32)),   // Dark Mica

                AppSystemBackdrop.Acrylic => theme == AppTheme.Light
                    ? new AcrylicBrush { TintColor = Colors.White, TintOpacity = 0.8 }
                    : new AcrylicBrush { TintColor = Colors.Black, TintOpacity = 0.8 },

                _ => theme == AppTheme.Light
                    ? GetCachedSolidBrush(Colors.White)
                    : GetCachedSolidBrush(Colors.Black)
            };
        }
        #endregion

        private static readonly ConcurrentDictionary<Color, SolidColorBrush> _solidBrushCache = new();

        // 预定义颜色常量
        private static readonly Color _activeLightForeground = Color.FromArgb(255, 0, 0, 0);       // 纯黑
        private static readonly Color _activeDarkForeground = Color.FromArgb(255, 255, 255, 255); // 纯白
        private static readonly Color _inactiveForeground = Color.FromArgb(255, 120, 120, 120);   // 非活动灰
        private static readonly Color _hoverLightBackground = Color.FromArgb(24, 0, 0, 0);         // 浅色悬停
        private static readonly Color _hoverDarkBackground = Color.FromArgb(24, 255, 255, 255);    // 深色悬停

        // 副标题颜色
        private static readonly Color _subtitleLightColor = Color.FromArgb(255, 102, 102, 102);   // 中灰
        private static readonly Color _subtitleDarkColor = Color.FromArgb(255, 153, 153, 153);    // 浅灰
    }
}
