using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;
using WinRT.Interop;
using WinUI3Localizer;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Container {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ToolContainer : WindowEx {
        public ToolContainer(
            string windowStyleType,
            AppTheme appTheme,
            SolidColorBrush windowCaptionForeground,
            SolidColorBrush windowCaptionForegroundDisabled) {
            _selectIdx2Content = [];
            _windowStyleType = windowStyleType;
            _appTheme = appTheme;
            _windowCaptionForeground = windowCaptionForeground;
            _windowCaptionForegroundDisabled = windowCaptionForegroundDisabled;            
            _localizer = LanguageUtil.LocalizerInstacne;

            this.InitializeComponent();

            SetWindowStyle();
            SetWindowTitleBar();
        }

        private void WindowEx_Activated(object sender, WindowActivatedEventArgs args) {
            if (args.WindowActivationState == WindowActivationState.Deactivated) {
                TitleTextBlock.Foreground = _windowCaptionForegroundDisabled;
            }
            else {
                TitleTextBlock.Foreground = _windowCaptionForeground;
            }
        }

        public void AddContent(string text, string tag, object content) {
            _selectIdx2Content[SelBar.Items.Count] = (tag, content);
            SelBar.Items.Add(new SelectorBarItem() {
                Name = $"s{SelBar.Items.Count}",
                Text = _localizer.GetLocalizedString(text),
                Tag = tag,
                IsSelected = SelBar.Items.Count == 0,
            });
        }

        public object GetContentByTag(string tag) {
            return _selectIdx2Content.FirstOrDefault(x => x.Value.Item1 == tag).Value.Item2;
        }

        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) {
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);

            var slideNavigationTransitionEffect = currentSelectedIndex - _previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

            _selectIdx2Content.TryGetValue(currentSelectedIndex, out (string, object) value);
            FrameComp.Navigate(typeof(PageContainer), value.Item2, new SlideNavigationTransitionInfo() { Effect = slideNavigationTransitionEffect });

            _previousSelectedIndex = currentSelectedIndex;
        }

        #region window title bar
        private void SetWindowStyle() {
            this.SystemBackdrop = _windowStyleType switch {
                "Mica" => new MicaBackdrop(),
                "Acrylic" => new DesktopAcrylicBackdrop(),
                _ => default,
            };
        }

        private void SetWindowTitleBar() {
            //ref: https://learn.microsoft.com/en-us/windows/apps/develop/title-bar?tabs=wasdk
            if (AppWindowTitleBar.IsCustomizationSupported()) {
                var titleBar = this.AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = _windowCaptionForeground.Color;

                AppTitleBar.Loaded += AppTitleBar_Loaded;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
                //this.Activated += WindowEx_Activated;
            }
            else {
                AppTitleBar.Visibility = Visibility.Collapsed;
                this.UseImmersiveDarkModeEx(_appTheme == AppTheme.Dark);
            }
        }

        private void AppTitleBar_Loaded(object sender, RoutedEventArgs e) {
            if (AppWindowTitleBar.IsCustomizationSupported()) {
                SetDragRegionForCustomTitleBar(this.AppWindow);
            }
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && this.AppWindow.TitleBar.ExtendsContentIntoTitleBar) {
                // Update drag region if the size of the title bar changes.
                SetDragRegionForCustomTitleBar(this.AppWindow);
            }
        }

        private void SetDragRegionForCustomTitleBar(AppWindow appWindow) {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && appWindow.TitleBar.ExtendsContentIntoTitleBar) {
                double scaleAdjustment = GetScaleAdjustment();

                RightPaddingColumn.Width = new GridLength(appWindow.TitleBar.RightInset / scaleAdjustment);
                LeftPaddingColumn.Width = new GridLength(appWindow.TitleBar.LeftInset / scaleAdjustment);

                List<Windows.Graphics.RectInt32> dragRectsList = [];

                Windows.Graphics.RectInt32 dragRectL;
                dragRectL.X = (int)((LeftPaddingColumn.ActualWidth) * scaleAdjustment);
                dragRectL.Y = 0;
                dragRectL.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectL.Width = (int)((IconColumn.ActualWidth
                                        + TitleColumn.ActualWidth
                                        + LeftDragColumn.ActualWidth) * scaleAdjustment);
                dragRectsList.Add(dragRectL);

                Windows.Graphics.RectInt32 dragRectR;
                dragRectR.X = (int)((LeftPaddingColumn.ActualWidth
                                    + IconColumn.ActualWidth
                                    + TitleTextBlock.ActualWidth
                                    + LeftDragColumn.ActualWidth) * scaleAdjustment);
                dragRectR.Y = 0;
                dragRectR.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectR.Width = (int)(RightDragColumn.ActualWidth * scaleAdjustment);
                dragRectsList.Add(dragRectR);

                Windows.Graphics.RectInt32[] dragRects = dragRectsList.ToArray();

                appWindow.TitleBar.SetDragRectangles(dragRects);
            }
        }

        private double GetScaleAdjustment() {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

            // Get DPI.
            int result = Native.GetDpiForMonitor(hMonitor, Native.Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
            if (result != 0) {
                throw new Exception("Could not get DPI for monitor.");
            }

            uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
            return scaleFactorPercent / 100.0;
        }
        #endregion

        private readonly string _windowStyleType;
        private readonly AppTheme _appTheme;
        private readonly SolidColorBrush _windowCaptionForegroundDisabled;
        private readonly SolidColorBrush _windowCaptionForeground;
        private readonly Dictionary<int, (string, object)> _selectIdx2Content;
        private readonly ILocalizer _localizer;
        private int _previousSelectedIndex = 0;
    }
}
