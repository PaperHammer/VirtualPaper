using Microsoft.Win32;
using NLog;
using OpenCvSharp;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Services.Interfaces;
using Timer = System.Timers.Timer;

namespace VirtualPaper.Services
{
    //ref:
    //https://gist.github.com/riverar/fd6525579d6bbafc6e48
    public class TaskbarService : ITaskbarService
    {
        public bool IsRunning { get; private set; } = false;

        public TaskbarService()
        {
            string? pgm = null;
            if ((pgm = CheckIncompatiblePrograms()) != null)
            {
                _logger.Info($"TranluscentTaskbar disabled, incompatible program found: {pgm}");
                _incompatibleProgramFound = true;
            }
            _timer.Interval = 500;
            _timer.Elapsed += (_, _) =>
            {
                SetTaskbarTransparent(_taskbarTheme);
            };

            SystemEvents.SessionSwitch += (s, e) => {
                if (e.Reason == SessionSwitchReason.SessionUnlock && IsRunning)
                {
                    ResetTaskbar();
                }
            };
        }

        public void Start(TaskbarTheme theme)
        {
            if (_incompatibleProgramFound)
            {
                return;
            }

            if (theme == TaskbarTheme.none)
            {
                Stop();
            }
            else
            {
                _timer.Stop();
                SetTheme(theme);
                ResetTaskbar();
                _timer.Start();
                IsRunning = true;
                _logger.Info("Taskbar theme service started.");
            }
        }

        public void Stop()
        {
            if (IsRunning)
            {
                _timer.Stop();
                ResetTaskbar();
                IsRunning = false;
                _logger.Info("Taskbar theme service stopped.");
            }
        }

        private void SetTheme(TaskbarTheme theme)
        {
            _taskbarTheme = theme;
            _logger.Info("Taskbar theme: {0}", theme);
            switch (_taskbarTheme)
            {
                case TaskbarTheme.none:
                    //accent.AccentState = AccentState.ACCENT_DISABLED;
                    break;
                case TaskbarTheme.clear:
                    _accentPolicyRegular.GradientColor = 16777215; //00FFFFFF
                    _accentPolicyRegular.AccentState = AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT;
                    break;
                case TaskbarTheme.blur:
                    _accentPolicyRegular.GradientColor = 0;
                    _accentPolicyRegular.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;
                    break;
                case TaskbarTheme.color:
                    //todo
                    break;
                case TaskbarTheme.fluent:
                    _accentPolicyRegular.GradientColor = 167772160; //A000000
                    _accentPolicyRegular.AccentState = AccentState.ACCENT_ENABLE_FLUENT;
                    break;
                case TaskbarTheme.wallpaper:
                    _accentPolicyRegular.GradientColor = Convert.ToUInt32(string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", 200, _accentColor.B, _accentColor.G, _accentColor.R), 16);
                    _accentPolicyRegular.AccentState = AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT;
                    break;
                case TaskbarTheme.wallpaperFluent:
                    _accentPolicyRegular.GradientColor = Convert.ToUInt32(string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", 125, _accentColor.B, _accentColor.G, _accentColor.R), 16);
                    _accentPolicyRegular.AccentState = AccentState.ACCENT_ENABLE_FLUENT;
                    break;
            }
        }

        public void SetAccentColor(Color color)
        {
            _accentColor = color;
            Start(_taskbarTheme);
        }

        private void SetTaskbarTransparent(TaskbarTheme theme)
        {
            if (theme == TaskbarTheme.none)
            {
                return;
            }

            var taskbars = GetTaskbars();
            if (taskbars.Count != 0)
            {
                var accentPtr = IntPtr.Zero;
                try
                {
                    var accentStructSize = Marshal.SizeOf(_accentPolicyRegular);
                    accentPtr = Marshal.AllocHGlobal(accentStructSize);
                    Marshal.StructureToPtr(_accentPolicyRegular, accentPtr, false);
                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                        SizeOfData = accentStructSize,
                        Data = accentPtr
                    };

                    foreach (var taskbar in taskbars)
                    {
                        _ = SetWindowCompositionAttribute(taskbar, ref data);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e.ToString());
                    Stop();
                }
                finally
                {
                    Marshal.FreeHGlobal(accentPtr);
                }
            }
        }

        #region dispose
        private bool _isDisposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Stop();
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region helpers
        private List<IntPtr> GetTaskbars()
        {
            IntPtr taskbar;
            var taskbars = new List<IntPtr>(2);
            //main taskbar
            if ((taskbar = Native.FindWindow("Shell_TrayWnd", null)) != IntPtr.Zero)
            {
                taskbars.Add(taskbar);
            }
            //secondary taskbar(s)
            if ((taskbar = Native.FindWindow("Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                taskbars.Add(taskbar);
                while ((taskbar = Native.FindWindowEx(IntPtr.Zero, taskbar, "Shell_SecondaryTrayWnd", IntPtr.Zero)) != IntPtr.Zero)
                {
                    taskbars.Add(taskbar);
                }
            }

            return taskbars;
        }

        private void ResetTaskbar()
        {
            foreach (var taskbar in GetTaskbars())
            {
                _ = Native.SendMessage(taskbar, (int)Native.WM.DWMCOMPOSITIONCHANGED, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public string CheckIncompatiblePrograms()
        {
            foreach (var item in incompatiblePrograms)
            {
                if (item.Value != null)
                {
                    try
                    {
                        Mutex? mutex = null;
                        try
                        {
                            if (Mutex.TryOpenExisting(item.Value, out mutex))
                            {
                                return item.Key;
                            }
                        }
                        finally
                        {
                            mutex?.Dispose();
                        }
                    }
                    catch { } //skipping
                }
                else
                {
                    try
                    {
                        var proc = Process.GetProcessesByName(item.Key);
                        if (proc.Length != 0)
                        {
                            return item.Key;
                        }
                    }
                    catch { } //skipping
                }
            }

            return null;
        }

        public Color GetAverageColor(string imgPath)
        {
            Mat rgbMat = new(imgPath, ImreadModes.Color);

            Scalar mean = Cv2.Mean(rgbMat);
            byte r = (byte)Math.Round(mean.Val0 * 255 / 255);
            byte g = (byte)Math.Round(mean.Val1 * 255 / 255);
            byte b = (byte)Math.Round(mean.Val2 * 255 / 255);

            return Color.FromArgb(r, g, b);
        }
        #endregion

        #region pinvoke 控制窗口视觉效果
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        /// <summary>
        /// 要修改的窗口属性类型
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]        
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            /// <summary>
            /// 窗口修饰
            /// </summary>
            WCA_ACCENT_POLICY = 19
        }

        /// <summary>
        /// 窗口强调效果
        /// </summary>
        internal enum AccentState
        {
            /// <summary>
            /// 禁用强调
            /// </summary>
            ACCENT_DISABLED = 0,
            /// <summary>
            /// 渐变效果
            /// </summary>
            ACCENT_ENABLE_GRADIENT = 1,
            /// <summary>
            /// 透明渐变
            /// </summary>
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            /// <summary>
            /// 模糊效果
            /// </summary>
            ACCENT_ENABLE_BLURBEHIND = 3,
            /// <summary>
            /// Fluent Design 风格（如 Acrylic 亚克力效果，即提供一种半透明、有深度感知的视觉体验）
            /// </summary>
            ACCENT_ENABLE_FLUENT = 4
        }

        /// <summary>
        /// 窗口强调策略
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            /// <summary>
            /// 具体的强调策略状态
            /// </summary>
            public AccentState AccentState;
            /// <summary>
            /// 标志位
            /// </summary>
            public int AccentFlags;
            /// <summary>
            /// 渐变颜色
            /// </summary>
            public uint GradientColor; //AABBGGRR
            /// <summary>
            /// 动画 ID 
            /// </summary>
            public int AnimationId;
        }
        #endregion

        private Color _accentColor = Color.FromArgb(0, 0, 0); // alpha = 0 完全透明
        private TaskbarTheme _taskbarTheme = TaskbarTheme.none;
        private AccentPolicy _accentPolicyRegular = new();
        private readonly bool _incompatibleProgramFound;
        private readonly Timer _timer = new();
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly static IDictionary<string, string> incompatiblePrograms = new Dictionary<string, string>() {
            {"TranslucentTB", "344635E9-9AE4-4E60-B128-D53E25AB70A7"},
            {"TaskbarX", null},
        };
    }
}
