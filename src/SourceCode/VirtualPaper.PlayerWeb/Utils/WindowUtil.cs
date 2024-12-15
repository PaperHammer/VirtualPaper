using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
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

        #region detail-page
        internal static void AddDetailsPage() {
            if (_details == null) {
                _details = new(_startArgs.WpBasicDataFilePath);

                _toolContainer.AddContent(Constants.LocalText.WpConfigViewMdoel_TextDetailedInfo, "Details", _details);
            }
        }
        #endregion

        #region effect-config-page
        internal static void AddEffectConfigPage() {
            if (_effectConfig == null) {
                _effectConfig = new(
                    _startArgs.WpEffectFilePathUsing,
                    _startArgs.WpEffectFilePathTemporary,
                    _startArgs.WpEffectFilePathTemplate);

                _effectConfig.DoubleValueChanged += EffectConfig_DoubleValueChanged;
                _effectConfig.IntValueChanged += EffectConfig_IntValueChanged;
                _effectConfig.BoolValueChanged += EffectConfig_BoolValueChanged;

                _effectConfig.ApplyChange += EffectConfig_ApplyChange;
                ToolWindowClose += _effectConfig.Closing;

                _toolContainer.AddContent(Constants.LocalText.WpConfigViewMdoel_TextWpEffectConfig, "EffectConfig", _effectConfig);
            }
        }

        private static void EffectConfig_ApplyChange(object sender, EventArgs e) {
            //_mainWindow.ExecuteScriptFunctionAsync(Fileds.ApplyFilter);
            //_mainWindow.ExecuteScriptFunctionAsync(Fileds.Play);
        }

        private static void EffectConfig_DoubleValueChanged(object sender, DoubleValueChangedEventArgs e) {
            _ = _mainWindow.ExecuteScriptFunctionAsync(Fileds.PropertyListener, e.PropertyName, e.Value);
        }

        private static void EffectConfig_IntValueChanged(object sender, IntValueChangedEventArgs e) {
            _ = _mainWindow.ExecuteScriptFunctionAsync(Fileds.PropertyListener, e.PropertyName, e.Value);
        }

        private static void EffectConfig_BoolValueChanged(object sender, BoolValueChangedEventArgs e) {
            _mainWindow.ExecuteCheckBoxSet(e.PropertyName, e.Value);
        }
        #endregion

        internal static void OpenToolWindow(StartArgs startArgs) {
            if (_toolContainer == null) {
                _startArgs = startArgs;
                _toolContainer = new(
                   startArgs.WindowStyleType,
                   startArgs.ApplicationTheme,
                   _mainWindow.AppWindow.TitleBar.ButtonForegroundColor,
                   _mainWindow.WindowCaptionForegroundDisabled,
                   _mainWindow.WindowCaptionForeground);

                _toolContainer.Closed += ToolContainer_Closed;
                static void ToolContainer_Closed(object _, WindowEventArgs __) {
                    _toolContainer.Closed -= ToolContainer_Closed;
                    ToolWindowClose?.Invoke(_toolContainer, EventArgs.Empty);

                    _effectConfig = null;
                    _toolContainer = null;
                    ToolWindowClose = null;
                }
                SetToolWindowParent();
                _toolContainer.Show();
            }
        }

        private static void SetToolWindowParent() {
            IntPtr toolHwnd = GetWindowHwnd(_toolContainer);
            Native.SetWindowLong(toolHwnd, Native.GWL_HWNDPARENT, GetWindowHwnd(_mainWindow));
        }

        internal static nint GetWindowHwnd(WindowEx windowEx) {
            return WindowNative.GetWindowHandle(windowEx);
        }

        internal static void CloseToolWindow() {
            _toolContainer?.Close();
        }

        private static EffectConfig _effectConfig;
        private static Details _details;
        private static ToolContainer _toolContainer;
        private readonly static MainWindow _mainWindow;
        private static StartArgs _startArgs;
    }
}
