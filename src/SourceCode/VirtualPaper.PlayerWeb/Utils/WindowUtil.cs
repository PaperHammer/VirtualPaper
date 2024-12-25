using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Models.EffectValue;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.UIComponent.Container;
using VirtualPaper.UIComponent.Data;
using WinRT.Interop;
using WinUIEx;

namespace VirtualPaper.PlayerWeb.Utils {
    internal class WindowUtil {
        static WindowUtil() {
            _mainWindow = App.MainWindowInstance;
            _appWindow = _mainWindow.AppWindow;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? DispatcherQueueController.CreateOnCurrentThread().DispatcherQueue;
        }

        internal static void InitWindowAsBackground() {
            _appWindow.Move(new Windows.Graphics.PointInt32() {
                X = -99999,
                Y = 0,
            });
            _appWindow.IsShownInSwitchers = false;
            _mainWindow.IsResizable = false;
        }

        #region detail-page
        internal static void AddDetailsPage() {
            if (_details == null) {
                _details = new(_startArgs.WpBasicDataFilePath);

                _toolContainer.AddContent(Constants.I18n.WpConfigViewMdoel_TextDetailedInfo, "Details", _details);
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

                _effectConfig.SaveAndApply += EffectConfig_SaveAndApply;
                _toolWindowClose += _effectConfig.Closing;

                _toolContainer.AddContent(Constants.I18n.WpConfigViewMdoel_TextWpEffectConfig, "EffectConfig", _effectConfig);
            }
        }

        private static void EffectConfig_SaveAndApply(object sender, EventArgs e) {
            if (_mainWindow._startArgs.IsPreview) {
                App.WriteToParent(new VirtualPaperApplyCmd());
                _mainWindow?.Close();
            }
            _toolContainer?.Close();
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

        internal static void ActiveToolWindow(StartArgs startArgs) {
            if (_toolContainer == null) {
                _startArgs = startArgs;
                _toolContainer = new(
                   startArgs.WindowStyleType,
                   startArgs.ApplicationTheme,
                   _mainWindow.WindowCaptionForeground,
                   _mainWindow.WindowCaptionForegroundDisabled);

                _toolContainer.Closed += ToolContainer_Closed;
                static void ToolContainer_Closed(object _, WindowEventArgs __) {
                    _toolContainer.Closed -= ToolContainer_Closed;
                    _toolWindowClose?.Invoke(_toolContainer, EventArgs.Empty);

                    _effectConfig = null;
                    _toolContainer = null;
                    _toolWindowClose = null;
                }
                SetToolWindowParent();
            }

            _toolContainer?.Show();
            _toolContainer?.BringToFront();
        }

        private static void SetToolWindowParent() {
            IntPtr toolHwnd = GetWindowHwnd(_toolContainer);
            Native.SetWindowLong(toolHwnd, Native.GWL_HWNDPARENT, GetWindowHwnd(_mainWindow));
        }

        private static nint GetWindowHwnd(WindowEx windowEx) {
            return WindowNative.GetWindowHandle(windowEx);
        }

        internal static void CloseToolWindow() {
            _toolContainer?.Close();
        }

        internal static bool IsToolContentNull() {
            return _effectConfig == null || _details == null;
        }

        private readonly static AppWindow _appWindow;
        private static EffectConfig _effectConfig;
        private static Details _details;
        private static ToolContainer _toolContainer;
        private readonly static MainWindow _mainWindow;
        private static StartArgs _startArgs;
        private readonly static DispatcherQueue _dispatcherQueue;
        private static EventHandler _toolWindowClose;
    }
}
