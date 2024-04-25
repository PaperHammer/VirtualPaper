using H.Hooks;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using VirtualPaper.Common.Utils.PInvoke;
using Windows.UI;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views.Utils
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ColorEyeDropWindow : WindowEx
    {
        public Color? SelectedColor { get; private set; }

        public ColorEyeDropWindow()
        {
            this.InitializeComponent();

            this.SystemBackdrop = new DesktopAcrylicBackdrop();
            //White border issue: https://github.com/microsoft/WindowsAppSDK/issues/2772
            this.IsTitleBarVisible = false;

            _hook = new LowLevelMouseHook()
            {
                GenerateMouseMoveEvents = true,
                Handling = true
            };
            _hook.Move += (_, e) =>
            {
                SetPosAndForeground(e.Position.X + 25, e.Position.Y + 25);
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    SelectedColor = GetColorAt(e.Position.X, e.Position.Y);
                    SetPreviewColor((Color)SelectedColor);
                });
            };
            _hook.Down += (_, e) =>
            {
                e.IsHandled = true; //Mouse click block
                if (e.Keys.Values.Contains(Key.MouseLeft))
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        SelectedColor = GetColorAt(e.Position.X, e.Position.Y);
                        this.Close();
                    });
                }
                else //discard
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        SelectedColor = null;
                        this.Close();
                    });
                }
            };
            _hook.Start();

            this.Closed += (_, _) =>
            {
                _hook?.Dispose();
            };

            this.Activated += (_, _) =>
            {
                //White border temp fix
                var styleCurrentWindowStandard = Native.GetWindowLongPtr(this.GetWindowHandle(), (int)Native.GWL.GWL_STYLE);
                var styleNewWindowStandard = styleCurrentWindowStandard.ToInt64() & ~((long)Native.WindowStyles.WS_THICKFRAME);
                if (Native.SetWindowLongPtr(new HandleRef(null, this.GetWindowHandle()), (int)Native.GWL.GWL_STYLE, (IntPtr)styleNewWindowStandard) == IntPtr.Zero)
                {
                    //fail
                }

                if (IsWindows11_OrGreater)
                {
                    //Bring back win11 rounded corner
                    var attribute = Native.DWMWINDOWATTRIBUTE.WindowCornerPreference;
                    var preference = Native.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
                    Native.DwmSetWindowAttribute(this.GetWindowHandle(), attribute, ref preference, sizeof(uint));
                }

                if (Native.GetCursorPos(out Native.POINT P))
                {
                    //Force redraw
                    Native.SetWindowPos(this.GetWindowHandle(), -1, P.X + 25, P.Y + 25, 1, 1, (int)Native.SetWindowPosFlags.SWP_SHOWWINDOW);
                }
            };
        }

        private void SetPreviewColor(Color color)
        {
            cBorder.Background = new SolidColorBrush(Color.FromArgb(
                              255,
                              color.R,
                              color.G,
                              color.B
                            ));
            cText.Text = $"rgb({color.R}, {color.G}, {color.B})";
        }

        public void SetPosAndForeground(int x, int y)
        {
            Native.SetWindowPos(this.GetWindowHandle(), -1, x, y, 0, 0, (int)Native.SetWindowPosFlags.SWP_NOSIZE);
        }

        #region helpers
        public static Color GetColorAt(int x, int y)
        {
            IntPtr desk = Native.GetDesktopWindow();
            IntPtr dc = Native.GetWindowDC(desk);
            try
            {
                int a = (int)Native.GetPixel(dc, x, y);
                return Color.FromArgb(255, (byte)((a >> 0) & 0xff), (byte)((a >> 8) & 0xff), (byte)((a >> 16) & 0xff));
            }
            finally
            {
                _ = Native.ReleaseDC(desk, dc);
            }
        }

        public static bool IsWindows11_OrGreater => Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
        #endregion

        private readonly LowLevelMouseHook _hook;
    }
}
