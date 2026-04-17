using System.Collections.Generic;
using Microsoft.UI.Xaml;
using VirtualPaper.UIComponent.Templates;

namespace VirtualPaper.UIComponent.Utils {
    public readonly record struct ArcWindowManagerKey(ArcWindowKey Key, string? BizType = null);

    // Helper class to allow the app to find the Window that contains an
    // arbitrary UIElement (GetWindowForElement).  To do this, we keep track
    // of all active Windows.  The app code must call ArcWindowManager.CreateWindow
    // rather than "new Window" so we can keep track of all the relevant
    // windows.  In the future, we would like to support this in platform APIs.
    public static partial class ArcWindowManager {
        public static ArcWindow? MainWindow => _mainWindow;
        public static FrameworkElement? MainWindowRootFe => _mainWindow?.ContentHost?.AppRoot;
        public static IReadOnlyList<ArcWindow> ActiveWindows => [.. _activeWindows.Values];

        static public void TrackWindow(ArcWindowManagerKey key, ArcWindow window) {
            if (window.IsMainWindow) {
                _mainWindow = window;
            }

            window.Closed += (sender, args) => {
                _activeWindows.Remove(key);
            };
            _activeWindows[key] = window;
        }

        internal static void UpdateWindowVisualState(ArcWindow window) {
            ArcWindowTitleBarUtil.UpdateTitleBar(window, ArcThemeUtil.GetFormatMainWindowTheme(), window.IsActive);
            if (window.AppNavView != null) ArcWindowTitleBarUtil.UpdateNaviVisualStates(window.AppNavView, ArcThemeUtil.GetFormatMainWindowTheme(), ArcThemeUtil.MainWindowBackdrop);
            ArcWindowTitleBarUtil.UpdateTitleBarVisualStates(window.ContentHost.AppTitleBar, ArcThemeUtil.GetFormatMainWindowTheme(), ArcThemeUtil.MainWindowBackdrop);
        }

        static public ArcWindow? GetWindowForElement(UIElement element) {
            if (element.XamlRoot != null) {
                foreach (ArcWindow window in _activeWindows.Values) {
                    if (element.XamlRoot == window.Content.XamlRoot) {
                        return window;
                    }
                }
            }
            return null;
        }

        public static ArcWindow? GetArcWindow(ArcWindowManagerKey key) {
            if (_activeWindows.TryGetValue(key, out var window)) return window;

            return null;
        }

        // get dpi for an element
        public static double GetRasterizationScaleForElement(UIElement element) {
            if (element.XamlRoot != null) {
                foreach (ArcWindow window in _activeWindows.Values) {
                    if (element.XamlRoot == window.Content.XamlRoot) {
                        return element.XamlRoot.RasterizationScale;
                    }
                }
            }
            return 0.0;
        }

        internal static void UpdateAllWindowTheme() {
            foreach (ArcWindow window in _activeWindows.Values) {
                _ = window.SetThemeAsync();
            }
        }

        internal static void Cleanup() {
            foreach (var window in _activeWindows.Values) {
                if (!window.IsMainWindow) {
                    window.Close();
                }
            }
        }

        private static ArcWindow? _mainWindow;
        private static readonly Dictionary<ArcWindowManagerKey, ArcWindow> _activeWindows = [];
    }

    public enum ArcWindowKey {
        Main,
        PlayerWebCore,
        PlayerWebCoreAdjust,
        PlayerWebCoreOnlyDetails,
        PlayerWebCoreDetailsEdit,
        Updater,
    }
}
