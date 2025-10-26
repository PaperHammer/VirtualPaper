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
            ThemeHelper.OnSystemThemeChanged += (sender, args) => {
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

        //static public void SetWindowMinSize(ArcWindow window, double width, double height) {
        //    if (window.Content is not FrameworkElement windowContent) {
        //        System.Diagnostics.Debug.WriteLine("Window content is not a FrameworkElement.");
        //        return;
        //    }

        //    if (windowContent.XamlRoot is null) {
        //        System.Diagnostics.Debug.WriteLine("Window content's XamlRoot is null.");
        //        return;
        //    }

        //    if (window.AppWindow.Presenter is not OverlappedPresenter presenter) {
        //        System.Diagnostics.Debug.WriteLine("Window's AppWindow.Presenter is not an OverlappedPresenter.");
        //        return;
        //    }

        //    var scale = windowContent.XamlRoot.RasterizationScale;
        //    var minWidth = width * scale;
        //    var minHeight = height * scale;
        //    presenter.PreferredMinimumWidth = (int)minWidth;
        //    presenter.PreferredMinimumHeight = (int)minHeight;
        //}

        //static public void FindVisualChildren(FrameworkElement parent, List<FrameworkElement> results) {
        //    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
        //        if (VisualTreeHelper.GetChild(parent, i) is FrameworkElement child) {
        //            if (child is TextBlock || child is ImageIcon || child is PathIcon)
        //                results.Add(child);

        //            FindVisualChildren(child, results);
        //        }
        //    }
        //}

        static public List<ArcWindow> ActiveWindows { get { return _activeWindows; } }
        private static readonly List<ArcWindow> _activeWindows = [];
    }
}
