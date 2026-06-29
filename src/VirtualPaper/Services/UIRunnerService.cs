using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.lang;
using VirtualPaper.Services.Interfaces;
using MessageBox = System.Windows.MessageBox;
using UAC = UACHelper.UACHelper;

namespace VirtualPaper.Services {
    public partial class UIRunnerService : IUIRunnerService {
        public event EventHandler<MessageType>? UISendCmd;

        public UIRunnerService() {
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
            if (VirtualPaper.Cores.AppUpdate.UpdateLock.IsPluginUpdating("UI") ||
                VirtualPaper.Cores.AppUpdate.UpdateLock.IsPluginUpdating("ML") ||
                VirtualPaper.Cores.AppUpdate.UpdateLock.IsPluginUpdating("Shaders")) {
                App.Log.Warn("UI startup blocked: update in progress");
                return;
            }

            if (_processUI != null) {
                try {
                    App.Log.Warn("UI is already running");
                    UISendCmd?.Invoke(this, MessageType.cmd_active);
                    //_processUI.StandardInput.WriteLine(JsonSerializer.Serialize(new VirtualPaperActiveCmd(), IpcMessageContext.Default.IpcMessage));
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
                        LanguageManager.Instance["Common_TextError"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        public void RestartUI() {
            if (_processUI != null) {
                try {
                    _processUI.Exited -= Proc_UI_Exited;
                    _processUI.OutputDataReceived -= Proc_OutputDataReceived;
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
                if (!_processUI.Responding || !_processUI.CloseMainWindow() || !_processUI.WaitForExit(3500)) {
                    _processUI.Kill();
                }
            }
            catch (Exception e) {
                App.Log.Error(e);
            }
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

            // Check for pending restart update when UI exits normally
            // (not during an update - UpdateLock would be set in that case)
            if (!UpdateLock.IsUpdating) {
                _ = CheckAndExecutePendingUpdateAsync();
            }
        }

        private async Task CheckAndExecutePendingUpdateAsync() {
            try {
                var restartService = App.Services.GetRequiredService<IRestartUpdateService>();
                var flagPath = Constants.CommonPaths.UpdateFlagPath;
                if (File.Exists(flagPath)) {
                    await restartService.ExecutePendingUpdateAsync();
                }
            }
            catch (Exception ex) {
                App.Log.Error("Failed to execute pending restart update", ex);
            }
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    try {
                        // If a pending restart update exists, close UI gracefully so
                        // Proc_UI_Exited fires and triggers ExecutePendingUpdateAsync.
                        var flagPath = VirtualPaper.Common.Constants.CommonPaths.UpdateFlagPath;
                        if (_processUI != null && File.Exists(flagPath)) {
                            CloseUI();
                            if (_processUI != null && !_processUI.HasExited) {
                                _processUI.WaitForExit(5000);
                            }
                        }
                        else {
                            _processUI?.Kill();
                        }
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
        private readonly string _fileName, _workingDir;
    }
}
