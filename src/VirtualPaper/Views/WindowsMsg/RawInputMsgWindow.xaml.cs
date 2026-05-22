using System.Windows;
using System.Windows.Interop;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils.Interfcaes;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.Views.WindowsMsg {
    /// <summary>
    /// 使用 DirectX RawInput 进行鼠标输入检索并响应到壁纸
    /// ref: https://docs.microsoft.com/en-us/windows/win32/inputdev/raw-input
    /// </summary>
    public partial class RawInputMsgWindow : Window, IRawInputMsg {
        #region setup               
        public InputForwardMode InputMode { get; private set; }
        public event EventHandler<MouseRawArgs>? MouseMoveRaw;
        public event EventHandler<MouseClickRawArgs>? MouseDownRaw;
        public event EventHandler<MouseClickRawArgs>? MouseUpRaw;
        public event EventHandler<KeyboardClickRawArgs>? KeyboardClickRaw;

        public RawInputMsgWindow(
            IUserSettingsService userSettings,
            IWallpaperControl wpControl,
            IMonitorManager displayManager) {
            this._userSettings = userSettings;
            this._wpControl = wpControl;
            this._monitorManager = displayManager;

            InitializeComponent();
            this.InputMode = InputForwardMode.mousekeyboard;
            wpControl.WallpaperReset += (s, e) => FindDesktopAndResetHandles();
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            HiddenWindowForWPF(helper);
        }

        private static void HiddenWindowForWPF(WindowInteropHelper helper) {
            var exStyle = Native.GetWindowLong(helper.Handle, Native.GWL_EXSTYLE);
            Native.SetWindowLong(helper.Handle, Native.GWL_EXSTYLE, exStyle | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        }

        private void FindDesktopAndResetHandles() {
            _workerWOrig = IntPtr.Zero;
            _progman = IntPtr.Zero;

            _progman = Native.FindWindow("Progman", null);
            var folderView = Native.FindWindowEx(_progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (folderView == IntPtr.Zero) {
                // 若桌面层不在 Progman 下，循环浏览 WorkerW 句柄并找到正确的句柄
                do {
                    _workerWOrig = Native.FindWindowEx(Native.GetDesktopWindow(), _workerWOrig, "WorkerW", null);
                    folderView = Native.FindWindowEx(_workerWOrig, IntPtr.Zero, "SHELLDLL_DefView", null);
                } while (folderView == IntPtr.Zero && _workerWOrig != IntPtr.Zero);
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e) {
            var windowInteropHelper = new WindowInteropHelper(this);
            var hwnd = windowInteropHelper.Handle;

            switch (InputMode) {
                case InputForwardMode.off:
                    this.Close();
                    break;
                case InputForwardMode.mouse:
                    // ExInputSink 使其即使在不在前台和异步时也能正常工作。
                    RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse,
                        RawInputDeviceFlags.ExInputSink, hwnd);
                    break;
                case InputForwardMode.mousekeyboard:
                    RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse,
                        RawInputDeviceFlags.ExInputSink, hwnd);
                    RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard,
                        RawInputDeviceFlags.ExInputSink, hwnd);
                    break;
            }

            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(Hook);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            switch (InputMode) {
                case InputForwardMode.off:
                    break;
                case InputForwardMode.mouse:
                    RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);
                    break;
                case InputForwardMode.mousekeyboard:
                    RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);
                    RawInputDevice.UnregisterDevice(HidUsageAndPage.Keyboard);
                    break;
            }
        }
        #endregion

        #region input forward
        protected IntPtr Hook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled) {
            // You can read inputs by processing the WM_INPUT message.
            if (msg == (int)Native.WM.INPUT) {
                // Create an RawInputData from the handle stored in lParam.
                var data = RawInputData.FromHandle(lparam);

                //You can identify the source device using Header.DeviceHandle or just Device.
                //var sourceDeviceHandle = _data.Header.DeviceHandle;
                //var sourceDevice = _data.Device;

                // The _data will be an instance of either RawInputMouseData, RawInputKeyboardData, or RawInputHidData.
                // They contain the raw input _data in their properties.
                switch (data) {
                    case RawInputMouseData mouse:
                        //RawInput only gives relative mouse movement value.. 
                        if (!Native.GetCursorPos(out Native.POINT P)) {
                            break;
                        }

                        switch (mouse.Mouse.Buttons) {
                            case RawMouseButtonFlags.LeftButtonDown:
                                RawInput_MouseDownRaw(P.X, P.Y, RawInputMouseBtn.Left);
                                break;
                            case RawMouseButtonFlags.LeftButtonUp:
                                RawInput_MouseUpRaw(P.X, P.Y, RawInputMouseBtn.Left);
                                break;
                            case RawMouseButtonFlags.RightButtonDown:
                                RawInput_MouseDownRaw(P.X, P.Y, RawInputMouseBtn.Right);
                                break;
                            case RawMouseButtonFlags.RightButtonUp:
                                RawInput_MouseUpRaw(P.X, P.Y, RawInputMouseBtn.Right);
                                break;
                            case RawMouseButtonFlags.None:
                                RawInput_MouseMoveRaw(P.X, P.Y);
                                break;
                            case RawMouseButtonFlags.MouseWheel:
                                //Disabled, not tested yet.
                                /*
                                https://github.com/ivarboms/game-engine/blob/master/Input/RawInput.cpp
                                Mouse wheel deltas are represented as multiples of 120.
                                MSDN: The delta was set to 120 to allow Microsoft or other vendors to build
                                finer-resolution wheels (a freely-rotating wheel with no notches) to send more
                                messages Per rotation, but with a smaller value in each message.
                                Because of this, the value is converted to a float in case a mouse's wheel
                                reports a value other than 120, in which case dividing by 120 would produce
                                a very incorrect value.
                                More info: http://social.msdn.microsoft.com/forums/en-US/gametechnologiesgeneral/thread/1deb5f7e-95ee-40ac-84db-58d636f601c7/
                                */

                                /*
                                // One wheel notch is represented as this delta (WHEEL_DELTA).
                                const float oneNotch = 120;

                                // Mouse wheel delta in multiples of WHEEL_DELTA (120).
                                float mouseWheelDelta = mouse.Mouse.RawButtons;

                                // Convert each notch from [-120, 120] to [-1, 1].
                                mouseWheelDelta = mouseWheelDelta / oneNotch;

                                MouseScrollSimulate(mouseWheelDelta);
                                */
                                break;
                        }
                        break;
                    case RawInputKeyboardData keyboard:
                        RawInput_KeyboardClickRaw(
                            (int)keyboard.Keyboard.WindowMessage,
                            (IntPtr)keyboard.Keyboard.VirutalKey,
                            keyboard.Keyboard.ScanCode,
                            keyboard.Keyboard.Flags != RawKeyboardFlags.Up);
                        KeyboardClickRaw?.Invoke(this, new KeyboardClickRawArgs(keyboard.Keyboard.VirutalKey));
                        break;
                }
            }

            return IntPtr.Zero;
        }

        private Monitor? _lastHoveredMonitor = null;

        //private void RawInput_MouseMoveRaw(int x, int y) {
        //    // 非桌面时，仅在 MouseInputMovAlways 开启时才转发 Move
        //    if (_userSettings.Settings.InputForward == InputForwardMode.off ||
        //        !IsDesktop() && !_userSettings.Settings.MouseInputMovAlways)
        //        return;

        //    try {
        //        var currentDisplay = _monitorManager.GetMonitorByPoint(new(x, y));

        //        // 光标切换到新显示器时，向旧显示器上的壁纸发送 MouseLeave
        //        if (_lastHoveredMonitor != null && !_lastHoveredMonitor.Equals(currentDisplay)) {
        //            foreach (var wallpaper in _wpControl.Wallpapers) {
        //                if (IsInputAllowed(wallpaper.Data.RType) &&
        //                    wallpaper.Monitor.Equals(_lastHoveredMonitor)) {
        //                    Native.PostMessageW(wallpaper.ProcWindowHandle, (int)Native.WM.MOUSELEAVE, IntPtr.Zero, IntPtr.Zero);
        //                    //wallpaper.SendMessage(new VirtualPaperMouseOutCmd());
        //                }
        //            }
        //        }
        //        _lastHoveredMonitor = currentDisplay;

        //        ForwardMouseToWallpapers(x, y, InputUtil.MouseMove);
        //        MouseMoveRaw?.Invoke(this, new MouseRawArgs(x, y));
        //    }
        //    catch (Exception e) {
        //        ArcLog.GetLogger<RawInputMsgWindow>().Error("Mouse Move Forwarding Error: " + e.Message);
        //    }
        //}
        private void RawInput_MouseMoveRaw(int x, int y) {
            if (_userSettings.Settings.InputForward == InputForwardMode.off ||
                !IsDesktop() && !_userSettings.Settings.MouseInputMovAlways)
                return;

            try {
                var currentDisplay = _monitorManager.GetMonitorByPoint(new(x, y));

                if (!currentDisplay.Equals(_lastHoveredMonitor)) {
                    foreach (var wallpaper in _wpControl.Wallpapers) {
                        if (!IsInputAllowed(wallpaper.Data.RType)) continue;

                        if (_lastHoveredMonitor != null &&
                            wallpaper.Monitor.Equals(_lastHoveredMonitor)) {
                            Native.PostMessageW(wallpaper.ProcWindowHandle,
                                (int)Native.WM.MOUSELEAVE, IntPtr.Zero, IntPtr.Zero);
                        }

                        if (wallpaper.Monitor.Equals(currentDisplay)) {
                            Native.PostMessageW(wallpaper.ProcWindowHandle,
                                (int)Native.WM.APP_MOUSEENTER, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                    _lastHoveredMonitor = currentDisplay; // ✅ 只在变化时赋值
                }

                ForwardMouseToWallpapers(x, y, InputUtil.MouseMove);
                MouseMoveRaw?.Invoke(this, new MouseRawArgs(x, y));
            }
            catch (Exception e) {
                ArcLog.GetLogger<RawInputMsgWindow>().Error("Mouse Move Forwarding Error: " + e.Message);
            }
        }

        private void RawInput_MouseDownRaw(int x, int y, RawInputMouseBtn btn) {
            if (_userSettings.Settings.InputForward == InputForwardMode.off || !IsDesktop())
                return;

            try {
                switch (btn) {
                    case RawInputMouseBtn.Left:
                        if (!InputUtil.IsMouseButtonsSwapped)
                            ForwardMouseToWallpapers(x, y, InputUtil.MouseLeftButtonDown);
                        else
                            ForwardMouseToWallpapers(x, y, InputUtil.MouseRightButtonDown);
                        break;
                    case RawInputMouseBtn.Right:
                        if (InputUtil.IsMouseButtonsSwapped)
                            ForwardMouseToWallpapers(x, y, InputUtil.MouseLeftButtonDown);
                        // 桌面右键已有系统上下文菜单，不额外转发右键 Down
                        break;
                }
                MouseDownRaw?.Invoke(this, new MouseClickRawArgs(x, y, btn));
            }
            catch (Exception e) {
                ArcLog.GetLogger<RawInputMsgWindow>().Error("Mouse Down Forwarding Error: " + e.Message);
            }
        }

        private void RawInput_MouseUpRaw(int x, int y, RawInputMouseBtn btn) {
            if (_userSettings.Settings.InputForward == InputForwardMode.off || !IsDesktop())
                return;

            try {
                switch (btn) {
                    case RawInputMouseBtn.Left:
                        if (!InputUtil.IsMouseButtonsSwapped)
                            ForwardMouseToWallpapers(x, y, InputUtil.MouseLeftButtonUp);
                        else
                            ForwardMouseToWallpapers(x, y, InputUtil.MouseRightButtonUp);
                        break;
                    case RawInputMouseBtn.Right:
                        if (InputUtil.IsMouseButtonsSwapped)
                            ForwardMouseToWallpapers(x, y, InputUtil.MouseLeftButtonUp);
                        break;
                }
                MouseUpRaw?.Invoke(this, new MouseClickRawArgs(x, y, btn));
            }
            catch (Exception e) {
                ArcLog.GetLogger<RawInputMsgWindow>().Error("Mouse Up Forwarding Error: " + e.Message);
            }
        }

        private void RawInput_KeyboardClickRaw(int msg, IntPtr wParam, int scanCode, bool isPressed) {
            // Don't forward when not on desktop.
            if (_userSettings.Settings.InputForward != InputForwardMode.mousekeyboard || !IsDesktop())
                return;

            try {
                if (!Native.GetCursorPos(out Native.POINT P))
                    return;

                var display = _monitorManager.GetMonitorByPoint(new(P.X, P.Y));
                foreach (var wallpaper in _wpControl.Wallpapers) {
                    if (IsInputAllowed(wallpaper.Data.RType) &&
                        (display.Equals(wallpaper.Monitor) || _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand)) {
                        InputUtil.ForwardMessageKeyboard(wallpaper.RealPlayerWindowHandle, msg, wParam, scanCode, isPressed);
                    }
                }
            }
            catch (Exception e) {
                ArcLog.GetLogger<RawInputMsgWindow>().Error("Keyboard Forwarding Error: " + e.Message);
            }
        }

        /// <summary>
        /// 将鼠标动作通过坐标本地化后分发给匹配的壁纸窗口。
        /// </summary>
        private void ForwardMouseToWallpapers(int x, int y, Action<IntPtr, int, int> forwardAction) {
            var display = _monitorManager.GetMonitorByPoint(new(x, y));
            var pos = _userSettings.Settings.WallpaperArrangement switch {
                WallpaperArrangement.Expand => InputUtil.ToMouseSpanLocal(x, y, _monitorManager.VirtualScreenBounds),
                _ => InputUtil.ToMouseDisplayLocal(x, y, display.Bounds),
            };

            foreach (var wallpaper in _wpControl.Wallpapers) {
                if (IsInputAllowed(wallpaper.Data.RType) &&
                    (wallpaper.Monitor.Equals(display) || _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand)) {
                    forwardAction(wallpaper.RealPlayerWindowHandle, pos.X, pos.Y);
                }
            }
        }

        #endregion //input forward

        #region helpers
        private static bool IsInputAllowed(RuntimeType category) {
            return category switch {
                RuntimeType.RImage or RuntimeType.RImage3D or RuntimeType.RWeb => true,
                RuntimeType.RVideo => false,
                _ => false,
            };
        }

        /// <summary>
        /// Is foreground wallpaper desktop.
        /// </summary>
        /// <returns></returns>
        private bool IsDesktop() {
            IntPtr hWnd = Native.GetForegroundWindow();
            return (IntPtr.Equals(hWnd, _workerWOrig) || IntPtr.Equals(hWnd, _progman));
        }

        #endregion

        IntPtr _progman, _workerWOrig;
        private readonly IUserSettingsService _userSettings;
        private readonly IWallpaperControl _wpControl;
        private readonly IMonitorManager _monitorManager;
    }

    public enum RawInputMouseBtn {
        Left,
        Right
    }

    public class MouseRawArgs(int x, int y) : EventArgs {
        public int X { get; } = x;
        public int Y { get; } = y;
    }

    public class MouseClickRawArgs(int x, int y, RawInputMouseBtn btn) : MouseRawArgs(x, y) {
        public RawInputMouseBtn Button { get; } = btn;
    }

    public class KeyboardClickRawArgs(int rawKeyboard) : EventArgs {
        public int Key { get; set; } = rawKeyboard;
    }
}
