using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.lang;
using VirtualPaper.Services.Interfaces;
using MessageBox = System.Windows.MessageBox;
using UAC = UACHelper.UACHelper;

namespace VirtualPaper.Services {
    public partial class UIRunnerService : IUIRunnerService {
        public UIRunnerService(
            IMonitorManager monitorManager) {
            _monitorManager = monitorManager;

            if (UAC.IsElevated) {
                App.Log.Warn("Process is running elevated, UI may not function properly.");
            }

            if (Constants.ApplicationType.IsMSIX) {
                _workingDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\"));
                _fileName = Path.Combine(_workingDir, Constants.ModuleName.UI);

            }
            else {
                _workingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.WorkingDir.UI);
                _fileName = Path.Combine(_workingDir, Constants.ModuleName.UI);
            }
        }

        public void ShowUI() {
            if (_processUI != null) {
                try {
                    App.Log.Warn("UI is already running");
                    _processUI.StandardInput.WriteLine(JsonSerializer.Serialize(new VirtualPaperActiveCmd(), IpcMessageContext.Default.IpcMessage));
                }
                catch (Exception e) {
                    App.Log.Error(e);
                }
            }
            else {
                try {
                    _processUI = new Process {
                        StartInfo = new ProcessStartInfo {
                            FileName = _fileName,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = false,
                            RedirectStandardError = false,
                            UseShellExecute = false,
                            WorkingDirectory = _workingDir,
                        },
                        EnableRaisingEvents = true
                    };
                    _processUI.Exited += Proc_UI_Exited;
                    _processUI.OutputDataReceived += Proc_OutputDataReceived;
                    _processUI.Start();
                    App.Jobs.AddProcess(_processUI.Id);

                    //winui writing debug information into output stream :/
                    //_processUI.BeginOutputReadLine();
                    //_processUI.BeginErrorReadLine();
                }
                catch (Exception e) {
                    App.Log.Error(e);
                    _processUI = null;
                    _ = MessageBox.Show(
                        $"{LanguageManager.Instance["UIRunnerService_VirtualPaperExceptionGeneral"]}\nEXCEPTION:\n{e.Message}",
                        LanguageManager.Instance["UIRunnerService_Error"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                if (!_isFirstRun) {
                    try {
                        SetWindowRect(_processUI, prevWindowRect);
                    }
                    catch (Exception ie) {
                        App.Log.Error($"Failed to restore windowrect: {ie.Message}");
                    }
                }
                _isFirstRun = false;
            }
        }

        public void RestartUI() {
            if (_processUI != null) {
                try {
                    _processUI.Exited -= Proc_UI_Exited;
                    _processUI.OutputDataReceived -= Proc_OutputDataReceived;
                    _ = Native.GetWindowRect(_processUI.MainWindowHandle, out prevWindowRect);
                    if (!_processUI.Responding || !_processUI.CloseMainWindow() || !_processUI.WaitForExit(500)) {
                        _processUI.Kill();
                    }
                    _processUI.Dispose();
                }
                catch (Exception e) {
                    App.Log.Error(e);
                }
                finally {
                    _processUI = null;
                }
            }
            ShowUI();
        }

        public void CloseUI() {
            if (_processUI == null)
                return;

            try {
                _ = Native.GetWindowRect(_processUI.MainWindowHandle, out prevWindowRect);
                if (!_processUI.Responding || !_processUI.CloseMainWindow() || !_processUI.WaitForExit(3500)) {
                    _processUI.Kill();
                }
            }
            catch (Exception e) {
                App.Log.Error(e);
            }
        }

        public void SaveRectUI() {
            if (_processUI == null)
                return;

            _ = Native.GetWindowRect(_processUI.MainWindowHandle, out prevWindowRect);
        }

        public nint GetUIHwnd() {
            if (_processUI == null) {
                return default;
            }

            return _processUI.MainWindowHandle;
        }

        public bool IsVisibleUI => _processUI != null && Native.IsWindowVisible(_processUI.MainWindowHandle);

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            //When the redirected stream is closed, a null line is sent to the event handler.
            if (!string.IsNullOrEmpty(e.Data)) {
                //Ref: https://github.com/cyanfish/grpc-dotnet-namedpipes/issues/8
                App.Log.Info($"UI: {e.Data}");
            }
        }

        private void Proc_UI_Exited(object? sender, EventArgs e) {
            if (_processUI == null) return;

            _processUI.Exited -= Proc_UI_Exited;
            _processUI.OutputDataReceived -= Proc_OutputDataReceived;
            _processUI.Dispose();
            _processUI = null;
        }

        #region helpers
        private void SetWindowRect(Process proc, Native.RECT rect) {
            ArgumentNullException.ThrowIfNull(proc);

            while (proc.WaitForInputIdle(-1) != true || proc.MainWindowHandle == IntPtr.Zero) {
                proc.Refresh();
            }

            Native.SetWindowPos(proc.MainWindowHandle,
                0,
                rect.Left,
                rect.Top,
                (rect.Right - rect.Left),
                (rect.Bottom - rect.Top),
                (int)Native.SetWindowPosFlags.SWP_SHOWWINDOW);

            //Monitor disconnected fallback.
            if (!IsOnScreen(proc.MainWindowHandle)) {
                Native.SetWindowPos(proc.MainWindowHandle,
                         0,
                         _monitorManager.PrimaryMonitor.Bounds.Left + 50,
                         _monitorManager.PrimaryMonitor.Bounds.Top + 50,
                         0,
                         0,
                         (int)Native.SetWindowPosFlags.SWP_NOSIZE);
            }
        }

        /// <summary>
        /// 检查窗口是否完全位于屏幕区域之外
        /// </summary>
        private bool IsOnScreen(IntPtr hwnd) {
            if (Native.GetWindowRect(hwnd, out Native.RECT winRect) != 0) {
                var rect = new Rectangle(
                    winRect.Left,
                    winRect.Top,
                    (winRect.Right - winRect.Left),
                    (winRect.Bottom - winRect.Top));
                return _monitorManager.Monitors.Any(s => s.WorkingArea.IntersectsWith(rect));
            }
            return false;
        }
        #endregion

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    try {
                        _processUI?.Kill();
                    }
                    catch { }
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private Process? _processUI;
        private readonly IMonitorManager _monitorManager;
        private bool _isFirstRun = true;
        private Native.RECT prevWindowRect = new() { Left = 50, Top = 50, Right = 925, Bottom = 925 };
        private readonly string _fileName, _workingDir;
    }
}
