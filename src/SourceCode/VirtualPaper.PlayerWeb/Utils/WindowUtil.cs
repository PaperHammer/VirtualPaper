using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common.Models.EffectValue;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.UIComponent.Container;
using VirtualPaper.UIComponent.Data;
using WinRT.Interop;
using WinUIEx;

namespace VirtualPaper.PlayerWeb.Utils {
    internal class WindowUtil {
        private static EventHandler ToolWindowClose;

        static WindowUtil() {
            _mainWindow = App.MainWindowInstance;
        }

        internal static AppWindow GetAppWindowForCurrentWindow() {
            IntPtr hwnd = GetWindowHwnd(_mainWindow);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

            return AppWindow.GetFromWindowId(windowId);
        }

        internal static void SetWindowAsBackground() {
            _mainWindow.IsResizable = false;

            AppWindow appWindow = GetAppWindowForCurrentWindow();
            appWindow.IsShownInSwitchers = false;
            appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }

        #region effect-config-window
        internal static void OpenEffectConfigWindow(StartArgs startArgs) {
            if (_effectConfig == null) {
                _effectConfig = new(
                    startArgs.WpEffectFilePathUsing,
                    startArgs.WpEffectFilePathTemporary,
                    startArgs.WpEffectFilePathTemplate);

                _effectConfig.DoubleValueChanged += EffectConfig_DoubleValueChanged;
                _effectConfig.IntValueChanged += EffectConfig_IntValueChanged;
                _effectConfig.BoolValueChanged += EffectConfig_BoolValueChanged;

                _effectConfig.ApplyChange += EffectConfig_ApplyChange;
                ToolWindowClose += _effectConfig.Closing;
            }

            OpenToolWindow(startArgs, _effectConfig);
        }

        private static void EffectConfig_ApplyChange(object sender, EventArgs e) {
            //_mainWindow.ExecuteScriptFunctionAsync(Fileds.ApplyFilter);
            //_mainWindow.ExecuteScriptFunctionAsync(Fileds.Play);
        }

        private static void EffectConfig_DoubleValueChanged(object sender, DoubleValueChangedEventArgs e) {
            _mainWindow.ExecuteScriptFunctionAsync(Fileds.PropertyListener, e.PropertyName, e.Value);
        }

        private static void EffectConfig_IntValueChanged(object sender, IntValueChangedEventArgs e) {
            _mainWindow.ExecuteScriptFunctionAsync(Fileds.PropertyListener, e.PropertyName, e.Value);
        }

        private static void EffectConfig_BoolValueChanged(object sender, BoolValueChangedEventArgs e) {
            _mainWindow.ExecuteCheckBoxSet(e.PropertyName, e.Value);
        }
        #endregion

        private static void OpenToolWindow(StartArgs startArgs, object content) {
            if (_toolContainer == null) {
                _toolContainer = new(
                   startArgs.WindowStyleType,
                   startArgs.ApplicationTheme,
                   _mainWindow.AppWindow.TitleBar.ButtonForegroundColor,
                   content,
                   (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"],
                   (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"]);
                
                _toolContainer.Closed += ToolContainer_Closed;
                static void ToolContainer_Closed(object sender, WindowEventArgs args) {
                    _toolContainer.Closed -= ToolContainer_Closed;
                    ToolWindowClose?.Invoke(_toolContainer, EventArgs.Empty);
                    
                    _effectConfig = null;
                    _toolContainer = null;
                    ToolWindowClose = null;
                }
                SetToolWindowActive();
                _toolContainer.Show();
            }
        }

        private static void SetToolWindowActive() {
            IntPtr toolHwnd = GetWindowHwnd(_toolContainer);
            //WindowId toolWindowId = Win32Interop.GetWindowIdFromWindow(toolHwnd);
            //AppWindow toolAppWindow = AppWindow.GetFromWindowId(toolWindowId);
            //OverlappedPresenter presenter = toolAppWindow.Presenter as OverlappedPresenter;

            Native.SetWindowLong(toolHwnd, Native.GWL_HWNDPARENT, GetWindowHwnd(_mainWindow));
            //presenter.IsModal = true;
        }

        internal static nint GetWindowHwnd(WindowEx windowEx) {
            return WindowNative.GetWindowHandle(windowEx);
        }

        internal static void CloseToolWindow() {
            _toolContainer?.Close();
        }

        private static EffectConfig _effectConfig;
        private static ToolContainer _toolContainer;
        private readonly static MainWindow _mainWindow;
    }
}
