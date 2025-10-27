using System.Collections.Generic;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils.Extensions;
using VirtualPaper.UIComponent.Windowing;

namespace VirtualPaper.UIComponent.Utils {
    // Helper class to allow the app to find the Window that contains an
    // arbitrary UIElement (GetWindowForElement).  To do this, we keep track
    // of all active Windows.  The app code must call WindowHelper.CreateWindow
    // rather than "new Window" so we can keep track of all the relevant
    // windows.  In the future, we would like to support this in platform APIs.
    public static partial class WindowHelper {
        static WindowHelper() {
            ThemeHelper.OnAppThemeChanged += (sender, args) => {
                foreach (ArcWindow window in _activeWindows) {
                    var currentTheme = ThemeHelper.GetCurrentTheme(window).ToAppTheme();
                    UpdateWindowVisualState(window);
                }
            };
        }

        static public void TrackWindow(ArcWindow window) {
            window.Closed += (sender, args) => {
                _activeWindows.Remove(window);
            };
            window.Activated += (sender, args) => {
                var isActive = args.WindowActivationState != WindowActivationState.Deactivated;
                var currentTheme = ThemeHelper.GetCurrentTheme(window).ToAppTheme();
                TitleBarHelper.UpdateTitleBar(window, window.TitleBarChildren, currentTheme, isActive);
            };
            if (window.Content is FrameworkElement root) {
                ThemeHelper.RegisterThemeRoot(root);
            }
            _activeWindows.Add(window);
        }

        public static void UpdateWindowVisualState(ArcWindow window) {
            var currentTheme = ThemeHelper.GetCurrentTheme(window).ToAppTheme();
            TitleBarHelper.UpdateTitleBar(window, window.TitleBarChildren, currentTheme, true);
            TitleBarHelper.UpdateNaviVisualStates(window.AppNavView, currentTheme, window.CurrentBackdrop);
            TitleBarHelper.UpdateTitleBarVisualStates(window.AppTitleBar, currentTheme, window.CurrentBackdrop);
        }

        static public ArcWindow GetWindowForElement(UIElement element) {
            if (element.XamlRoot != null) {
                foreach (ArcWindow window in _activeWindows) {
                    if (element.XamlRoot == window.Content.XamlRoot) {
                        return window;
                    }
                }
            }
            return null;
        }

        // get dpi for an element
        static public double GetRasterizationScaleForElement(UIElement element) {
            if (element.XamlRoot != null) {
                foreach (ArcWindow window in _activeWindows) {
                    if (element.XamlRoot == window.Content.XamlRoot) {
                        return element.XamlRoot.RasterizationScale;
                    }
                }
            }
            return 0.0;
        }

        internal static void UpdateThemeFromSys(AppTheme theme) {
            foreach (ArcWindow window in _activeWindows) {
                window.SetThemeAsync(theme);
            }
        }

        private static readonly List<ArcWindow> _activeWindows = [];
    }
}
