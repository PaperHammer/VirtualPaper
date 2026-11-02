using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Cores.AppUpdate.Specific;
using VirtualPaper.GrpcServers;
using VirtualPaper.lang;
using VirtualPaper.Services.Interfaces;
using MessageBox = System.Windows.MessageBox;
using UAC = UACHelper.UACHelper;

namespace VirtualPaper.Services {
    public partial class UIRunnerService : IUIRunnerService {
        public event EventHandler<MessageType>? UISendCmd;

        public UIRunnerService(IJobService jobService) {
            _jobService = jobService;

            if (UAC.IsElevated) {
                ArcLog.GetLogger<UIRunnerService>().Warn("Process is running elevated, UI may not work properly.");
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

        public async Task ShowUIAsync() {
            await UpdateLock.WaitAllAsync();
            lock (_processLock) {
                if (_isDisposed || App.IsShuttingDown) return;
                if (_processUI?.HasExited == true) ReleaseProcess();
                if (_processUI != null) {
                    try {
                        ArcLog.GetLogger<UIRunnerService>().Warn("UI is already running");
                        UISendCmd?.Invoke(this, MessageType.cmd_active);
                        //_processUI.StandardInput.WriteLine(JsonSerializer.Serialize(new VirtualPaperActiveCmd(), IpcMessageContext.Default.IpcMessage));
                    }
                    catch (Exception e) {
                        ArcLog.GetLogger<UIRunnerService>().Error(e);
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
                        _jobService.AddProcess(_processUI.Id, PluginName.UI);

                        //winui writing debug information into output stream :/
                        //_processUI.BeginOutputReadLine();
                        //_processUI.BeginErrorReadLine();
                    }
                    catch (Exception e) {
                        ArcLog.GetLogger<UIRunnerService>().Error(e);
                        _processUI = null;
                        _ = MessageBox.Show(
                            $"{LanguageManager.Instance["UIRunnerService_VirtualPaperExceptionGeneral"]}\nEXCEPTION:\n{e.Message}",
                            LanguageManager.Instance["Common_TextError"],
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }

        public void RestartUI() {
            lock (_processLock) {
                if (_isDisposed || App.IsShuttingDown) return;
                if (_processUI != null) {
                    try {
                        _processUI.Exited -= Proc_UI_Exited;
                        _processUI.OutputDataReceived -= Proc_OutputDataReceived;
                        if (!_processUI.HasExited && (!_processUI.Responding || !_processUI.CloseMainWindow() || !_processUI.WaitForExit(500))) {
                            _processUI.Kill();
                        }
                    }
                    catch (Exception e) {
                        ArcLog.GetLogger<UIRunnerService>().Error(e);
                    }
                    finally {
                        ReleaseProcess();
                    }
                }
            }
            _ = ShowUIAsync();
        }

        public void CloseUI() {
            lock (_processLock) {
                if (_processUI == null || _processUI.HasExited)
                    return;

                try {
                    if (!_processUI.Responding || !_processUI.CloseMainWindow() || !_processUI.WaitForExit(3500)) {
                        _processUI.Kill();
                    }
                }
                catch (Exception e) {
                    ArcLog.GetLogger<UIRunnerService>().Error(e);
                }
            }
        }

        public void SendCloseCmd() {
            UISendCmd?.Invoke(this, MessageType.cmd_close);
        }

        public async Task<bool> RequestUICloseAsync() {
            if (!IsVisibleUI) {
                return true; // UI 已经关闭
            }

            // 通过双向流发送关闭请求并等待响应
            return await TwoWayServer.RequestUICloseAsync(TimeSpan.FromSeconds(30));
        }

        public nint GetUIHwnd() {
            lock (_processLock) {
                if (_processUI == null || _processUI.HasExited) {
                    return default;
                }

                return _processUI.MainWindowHandle;
            }
        }

        public bool IsVisibleUI => Native.IsWindowVisible(GetUIHwnd());

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            //When the redirected stream is closed, a null line is sent to the event handler.
            if (!string.IsNullOrEmpty(e.Data)) {
                //Ref: https://github.com/cyanfish/grpc-dotnet-namedpipes/issues/8
                ArcLog.GetLogger<UIRunnerService>().Info($"UI: {e.Data}");
            }
        }

        private void Proc_UI_Exited(object? sender, EventArgs e) {
            lock (_processLock) {
                // An exit callback may already be queued when RestartUI detaches
                // the handler. It must never clear or stop the replacement process.
                if (sender is not Process process || !ReferenceEquals(process, _processUI)) return;

                var pid = process.Id;
                ReleaseProcess();

                if (_isDisposed || App.IsShuttingDown) return;

                _jobService.StopPlugin(pid);

                // Check for pending plugin update when UI exits normally
                // (not during an update - UpdateLock would be set in that case)
                // Installer update is handled in App.ShutDownAsync()
                if (!UpdateLock.IsAnyLocked) {
                    var pluginFlagPath =Constants.CommonPaths.UpdateFlagPath;
                    if (File.Exists(pluginFlagPath)) {
                        _jobService.PluginsUpdateFinishedEvent += (s, e) => {
                            _ = ShowUIAsync();
                        };
                        var restartService = App.Services.GetRequiredService<IPluginsUpdateService>();
                        _ = restartService.ExecutePendingPluginUpdateWithWindowAsync();
                    }
                }
            }
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            lock (_processLock) {
                if (!_isDisposed) {
                    _isDisposed = true;
                    if (disposing) {
                        try {
                            if (_processUI != null) {
                                // Prefer a normal close when a pending update exists,
                                // but never start another UI after this service is disposed.
                                var flagPath = VirtualPaper.Common.Constants.CommonPaths.UpdateFlagPath;
                                if (File.Exists(flagPath)) {
                                    CloseUI();
                                }
                                else {
                                    _processUI.Kill();
                                }
                            }
                        }
                        catch { }
                        finally { ReleaseProcess(); }
                    }
                }
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private void ReleaseProcess() {
            var process = _processUI;
            _processUI = null;
            if (process == null) return;
            process.Exited -= Proc_UI_Exited;
            process.OutputDataReceived -= Proc_OutputDataReceived;
            process.Dispose();
        }

        private readonly object _processLock = new();
        private Process? _processUI;
        private readonly string _fileName, _workingDir;
        private readonly IJobService _jobService;
    }
}
