using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Container {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FlyoutWindow : WindowEx {
        public FlyoutWindow(List<FrameworkElement> itemSources, PointerRoutedEventArgs e) {
            this.InitializeComponent();

            SetWindowStyle();
            SetWindowTitleBar();

            this._e = e;
            this.SelectableItems.ItemsSource = itemSources;
            this.Content.PointerMoved += Content_PointerMoved;
            this.Content.PointerReleased += Content_PointerReleased;
        }

        private void PointerPressedAtInit(PointerRoutedEventArgs e) {
            if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;

            _isDragging = true;
            _startPoint = e.GetCurrentPoint(this.Content).Position;
        }

        private void Content_PointerReleased(object sender, PointerRoutedEventArgs e) {
            _isDragging = false;
        }

        private void Content_PointerMoved(object sender, PointerRoutedEventArgs e) {
            if (_isDragging) {
                var currentPoint = e.GetCurrentPoint(this.Content).Position;
                var deltaX = currentPoint.X - _startPoint.X;
                var deltaY = currentPoint.Y - _startPoint.Y;

                PointInt32 newPosition = this.AppWindow.Position;
                newPosition.X += (int)deltaX;
                newPosition.Y += (int)deltaY;
                this.AppWindow.Move(new((int)currentPoint.X, (int)currentPoint.Y));

                _startPoint = currentPoint;
            }
        }

        private void WindowEx_Activated(object sender, WindowActivatedEventArgs args) {
            if (args.WindowActivationState == WindowActivationState.Deactivated) {
                TitleTextBlock.Foreground = ResourcesUtil.GetBrush(Constants.ColorKey.WindowCaptionForegroundDisabled);
            }
            else {
                TitleTextBlock.Foreground = ResourcesUtil.GetBrush(Constants.ColorKey.WindowCaptionForeground);
            }

            if (_isFirstRun) {
                PointerPressedAtInit(_e);
                _isFirstRun = false;
            }
        }

        #region window title bar
        private void SetWindowStyle() {
            this.SystemBackdrop = ObjectProvider.GetRequiredService<IUserSettingsClient>().Settings.SystemBackdrop switch {
                AppSystemBackdrop.Mica => new MicaBackdrop(),
                AppSystemBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
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
                titleBar.ButtonForegroundColor = ResourcesUtil.GetBrush(Constants.ColorKey.WindowCaptionForeground).Color;

                AppTitleBar.Loaded += AppTitleBar_Loaded;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
                //this.Activated += WindowEx_Activated;
            }
            else {
                AppTitleBar.Visibility = Visibility.Collapsed;
                this.UseImmersiveDarkModeEx(ObjectProvider.GetRequiredService<IUserSettingsClient>().Settings.ApplicationTheme == AppTheme.Dark);
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
                double scaleAdjustment = SystemUtil.GetScaleAdjustment(this);

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
        #endregion

        private bool _isDragging = false;
        private Point _startPoint = default;
        private bool _isFirstRun = true;
        private readonly PointerRoutedEventArgs _e;
    }
}
