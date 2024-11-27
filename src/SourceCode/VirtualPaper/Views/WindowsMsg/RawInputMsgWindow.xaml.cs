using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using NLog;
using System.Windows;
using System.Windows.Interop;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using Point = System.Drawing.Point;

namespace VirtualPaper.Views.WindowsMsg
{
    /// <summary>
    /// 使用 DirectX RawInput 进行鼠标输入检索并响应到壁纸
    /// ref: https://docs.microsoft.com/en-us/windows/win32/inputdev/raw-input
    /// </summary>
    public partial class RawInputMsgWindow : Window
    {
        #region setup               
        public InputForwardMode InputMode { get; private set; }
        public event EventHandler<MouseRawArgs>? MouseMoveRaw;
        public event EventHandler<MouseClickRawArgs>? MouseDownRaw;
        public event EventHandler<MouseClickRawArgs>? MouseUpRaw;
        public event EventHandler<KeyboardClickRawArgs>? KeyboardClickRaw;
        //public event EventHandler<KeyboardClickRawArgs>? KeyboardUpRaw;

        public RawInputMsgWindow(
            IUserSettingsService userSettings, 
            IWallpaperControl wpControl, 
            IMonitorManager displayManager)
        {
            this._userSettings = userSettings;
            this._wpControl = wpControl;
            this._monitorManager = displayManager;

            InitializeComponent();
            this.InputMode = InputForwardMode.mousekeyboard;
            wpControl.WallpaperReset += (s, e) => FindDesktopAndResetHandles();
        }

        //public Point GetMousePos()
        //{
        //    if (!Native.GetCursorPos(out Native.POINT P))
        //    {
        //        return Point.Empty;
        //    }

        //    var display = _monitorManager.GetMonitorByPoint(new(P.X, P.Y));
        //    var mouse = CalculateMousePos(P.X, P.Y, display, _userSettings.Settings.WallpaperArrangement);

        //    return mouse;
        //}

        //private void Window_Activated(object sender, EventArgs e)
        //{
        //    this.Hide();
        //}

        private void FindDesktopAndResetHandles()
        {
            _workerWOrig = IntPtr.Zero;
            _progman = IntPtr.Zero;

            _progman = Native.FindWindow("Progman", null);
            var folderView = Native.FindWindowEx(_progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (folderView == IntPtr.Zero)
            {
                // 若桌面层不在 Progman 下，循环浏览 WorkerW 句柄并找到正确的句柄
                do
                {
                    _workerWOrig = Native.FindWindowEx(Native.GetDesktopWindow(), _workerWOrig, "WorkerW", null);
                    folderView = Native.FindWindowEx(_workerWOrig, IntPtr.Zero, "SHELLDLL_DefView", null);
                } while (folderView == IntPtr.Zero && _workerWOrig != IntPtr.Zero);
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var windowInteropHelper = new WindowInteropHelper(this);
            var hwnd = windowInteropHelper.Handle;

            switch (InputMode)
            {
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            switch (InputMode)
            {
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
        protected IntPtr Hook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            // You can read inputs by processing the WM_INPUT message.
            if (msg == (int)Native.WM.INPUT)
            {
                // Create an RawInputData from the handle stored in lParam.
                var data = RawInputData.FromHandle(lparam);

                //You can identify the source device using Header.DeviceHandle or just Device.
                //var sourceDeviceHandle = data.Header.DeviceHandle;
                //var sourceDevice = data.Device;

                // The data will be an instance of either RawInputMouseData, RawInputKeyboardData, or RawInputHidData.
                // They contain the raw input data in their properties.
                switch (data)
                {
                    case RawInputMouseData mouse:
                        //RawInput only gives relative mouse movement value.. 
                        if (!Native.GetCursorPos(out Native.POINT P))
                        {
                            break;
                        }

                        switch (mouse.Mouse.Buttons)
                        {
                            case RawMouseButtonFlags.LeftButtonDown:
                                ForwardMessageMouse(P.X, P.Y, (int)Native.WM.LBUTTONDOWN, (IntPtr)0x0001);
                                MouseDownRaw?.Invoke(this, new MouseClickRawArgs(P.X, P.Y, RawInputMouseBtn.Left));
                                break;
                            case RawMouseButtonFlags.LeftButtonUp:
                                ForwardMessageMouse(P.X, P.Y, (int)Native.WM.LBUTTONUP, (IntPtr)0x0001);
                                MouseUpRaw?.Invoke(this, new MouseClickRawArgs(P.X, P.Y, RawInputMouseBtn.Left));
                                break;
                            case RawMouseButtonFlags.RightButtonDown:
                                //issue: click being skipped; desktop already has its own rightclick contextmenu.
                                //ForwardMessage(M.X, M.Y, (int)Native.WM.RBUTTONDOWN, (IntPtr)0x0002);
                                MouseDownRaw?.Invoke(this, new MouseClickRawArgs(P.X, P.Y, RawInputMouseBtn.Right));
                                break;
                            case RawMouseButtonFlags.RightButtonUp:
                                //issue: click being skipped; desktop already has its own rightclick contextmenu.
                                //ForwardMessage(M.X, M.Y, (int)Native.WM.RBUTTONUP, (IntPtr)0x0002);
                                MouseUpRaw?.Invoke(this, new MouseClickRawArgs(P.X, P.Y, RawInputMouseBtn.Right));
                                break;
                            case RawMouseButtonFlags.None:
                                ForwardMessageMouse(P.X, P.Y, (int)Native.WM.MOUSEMOVE, (IntPtr)0x0020);
                                MouseMoveRaw?.Invoke(this, new MouseRawArgs(P.X, P.Y));
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
                        ForwardMessageKeyboard((int)keyboard.Keyboard.WindowMessage,
                            (IntPtr)keyboard.Keyboard.VirutalKey, keyboard.Keyboard.ScanCode,
                            (keyboard.Keyboard.Flags != RawKeyboardFlags.Up));
                        KeyboardClickRaw?.Invoke(this, new KeyboardClickRawArgs(keyboard.Keyboard.VirutalKey));
                        break;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Forwards the keyboard message to the required wallpaper window based on given cursor location.<br/>
        /// Skips if desktop is not focused.
        /// </summary>
        /// <param name="msg">key press msg.</param>
        /// <param name="wParam">Virtual-Key code.</param>
        /// <param name="scanCode">OEM code of the key.</param>
        /// <param name="isPressed">Key is pressed.</param>
        private void ForwardMessageKeyboard(int msg, IntPtr wParam, int scanCode, bool isPressed)
        {
            try
            {
                //Don't forward when not on desktop.
                if (_userSettings.Settings.InputForward == InputForwardMode.mousekeyboard && IsDesktop())
                {
                    //Detect active wp based on cursor pos, better way to do this?
                    if (!Native.GetCursorPos(out Native.POINT P))
                        return;

                    var display = _monitorManager.GetMonitorByPoint(new(P.X, P.Y));
                    foreach (var wallpaper in _wpControl.Wallpapers)
                    {
                        if (IsInputAllowed(wallpaper.Data.RType))
                        {
                            if (display.Equals(wallpaper.Monitor) || _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand)
                            {
                                //ref:
                                //https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-keydown
                                //https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-keyup
                                uint lParam = 1u; //press
                                lParam |= (uint)scanCode << 16; //oem code
                                lParam |= 1u << 24; //extended key
                                lParam |= 0u << 29; //context code; Note: Alt key combos wont't work
                                /* Same as:
                                 * lParam = isPressed ? (lParam |= 0u << 30) : (lParam |= 1u << 30); //prev key state
                                 * lParam = isPressed ? (lParam |= 0u << 31) : (lParam |= 1u << 31); //transition state
                                 */
                                lParam = isPressed ? lParam : (lParam |= 3u << 30);
                                Native.PostMessageW(wallpaper.Handle, msg, wParam, (UIntPtr)lParam);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error("Keyboard Forwarding Errors:" + e.Message);
            }
        }

        /// <summary>
        /// Forwards the mouse message to the required wallpaper window based on given cursor location.<br/>
        /// Skips if apps are in foreground.
        /// </summary>
        /// <param name="x">Cursor pos x</param>
        /// <param name="y">Cursor pos y</param>
        /// <param name="msg">mouse message</param>
        /// <param name="wParam">additional msg parameter</param>
        private void ForwardMessageMouse(int x, int y, int msg, IntPtr wParam)
        {
            if (_userSettings.Settings.InputForward == InputForwardMode.off)
            {
                return;
            }
            else if (!IsDesktop()) //Don't forward when not on desktop.
            {
                if (msg != (int)Native.WM.MOUSEMOVE || !_userSettings.Settings.MouseInputMovAlways)
                {
                    return;
                }
            }

            try
            {
                var display = _monitorManager.GetMonitorByPoint(new(x, y));
                var mouse = CalculateMousePos(x, y, display, _userSettings.Settings.WallpaperArrangement);
                foreach (var wallpaper in _wpControl.Wallpapers)
                {
                    if (IsInputAllowed(wallpaper.Data.RType))
                    {
                        if (wallpaper.Monitor.Equals(display) || _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand)
                        {
                            //The low-order word specifies the x-coordinate of the cursor, the high-order word specifies the y-coordinate of the cursor.
                            //ref: https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-mousemove
                            uint lParam = Convert.ToUInt32(mouse.Y);
                            lParam <<= 16;
                            lParam |= Convert.ToUInt32(mouse.X);
                            Native.PostMessageW(wallpaper.Handle, msg, wParam, (UIntPtr)lParam);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error("Mouse Forwarding Errors:" + e.Message);
            }
        }

        #endregion //input forward

        #region helpers
        /// <summary>
        /// 将全局鼠标光标位置值转换为每个显示器的本地化值。
        /// </summary>
        /// <param name="x">Cursor pos x</param>
        /// <param name="y">Cursor pos y</param>
        /// <param name="monitor">Target monitor device</param>
        /// <returns>本地化的游标值</returns>
        private Point CalculateMousePos(int x, int y, IMonitor monitor, WallpaperArrangement arrangement)
        {
            if (_monitorManager.IsMultiScreen())
            {
                if (arrangement == WallpaperArrangement.Expand)
                {
                    var screenArea = _monitorManager.VirtualScreenBounds;
                    x -= screenArea.Location.X;
                    y -= screenArea.Location.Y;
                }
                else // 每个监视器或复制模式
                {
                    x += -1 * monitor.Bounds.X;
                    y += -1 * monitor.Bounds.Y;
                }
            }
            return new Point(x, y);
        }

        private static bool IsInputAllowed(RuntimeType category)
        {
            return category switch
            {                
                RuntimeType.RImage => false,
                RuntimeType.RImage3D => false,
                RuntimeType.RVideo => false,
                _ => false,
            };
        }

        /// <summary>
        /// Is foreground wallpaper desktop.
        /// </summary>
        /// <returns></returns>
        private bool IsDesktop()
        {
            IntPtr hWnd = Native.GetForegroundWindow();
            return (IntPtr.Equals(hWnd, _workerWOrig) || IntPtr.Equals(hWnd, _progman));
        }

        #endregion

        IntPtr _progman, _workerWOrig;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IUserSettingsService _userSettings;
        private readonly IWallpaperControl _wpControl;
        private readonly IMonitorManager _monitorManager;
    }

    public enum RawInputMouseBtn
    {
        Left,
        Right
    }

    public class MouseRawArgs(int x, int y) : EventArgs
    {
        public int X { get; } = x;
        public int Y { get; } = y;
    }

    public class MouseClickRawArgs(int x, int y, RawInputMouseBtn btn) : MouseRawArgs(x, y)
    {
        public RawInputMouseBtn Button { get; } = btn;
    }

    public class KeyboardClickRawArgs(int rawKeyboard) : EventArgs
    {
        public int Key { get; set; } = rawKeyboard;
    }
}
