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

        public UIRunnerService(IJobService jobService) : this(jobService, TimeSpan.FromSeconds(5)) { }

        internal UIRunnerService(IJobService jobService, TimeSpan uiExitTimeout) {
            _jobService = jobService;
            _uiExitTimeout = uiExitTimeout;

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
            while (true) {
                await UpdateLock.WaitAllAsync();
                Task<bool>? exitTask;
                Task? updateTask;
                lock (_processLock) {
                    if (_isDisposed || App.IsShuttingDown) return;
                    // Finish normal exit handling even if its queued callback has not run yet.
                    // A pending update owns reopening the UI after it completes.
                    if (_processUI is { HasExited: true } exitedProcess && HandleUiExited(exitedProcess)) return;
                    updateTask = _uiUpdateTask is { IsCompleted: false } ? _uiUpdateTask : null;
                    exitTask = _uiExitTask;
                    if (updateTask == null && exitTask == null && _processUI != null) {
                        try {
                            ArcLog.GetLogger<UIRunnerService>().Warn("UI is already running");
                            UISendCmd?.Invoke(this, MessageType.cmd_active);
                            //_processUI.StandardInput.WriteLine(JsonSerializer.Serialize(new VirtualPaperActiveCmd(), IpcMessageContext.Default.IpcMessage));
                        }
                        catch (Exception e) {
                            ArcLog.GetLogger<UIRunnerService>().Error(e);
                        }
                    }
                    else if (updateTask == null && exitTask == null) {
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
                if (updateTask != null) {
                    await updateTask.ConfigureAwait(false);
                    continue;
                }
                if (exitTask == null) return;
                // Await outside the lock so the exit callback and watchdog can finish.
                if (!await exitTask.ConfigureAwait(false))
                    throw new InvalidOperationException("The previous UI process could not be stopped; a replacement was not started.");
            }
        }

        public void NotifyUIClosing(int processId) {
            lock (_processLock) {
                if (_isDisposed || _processUI == null || _processUI.Id != processId
                    || _processUI.HasExited || _uiExitTask != null) return;
                _uiExitCancellation = new CancellationTokenSource();
                // Never resume an opener inline inside ReleaseProcess/exit cleanup.
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _uiExitTask = completion.Task;
                _ = MonitorUiExitAsync(_processUI, _uiExitCancellation.Token, completion);
            }
        }

        private async Task MonitorUiExitAsync(Process process, CancellationToken cancellationToken,
            TaskCompletionSource<bool> completion) {
            bool success = true;
            try {
                await Task.Delay(_uiExitTimeout, cancellationToken).ConfigureAwait(false);
                lock (_processLock) {
                    if (_isDisposed || !ReferenceEquals(process, _processUI)) return;
                    if (!process.HasExited) {
                        ArcLog.GetLogger<UIRunnerService>().Warn($"UI process {process.Id} did not exit within {_uiExitTimeout.TotalSeconds}s; terminating it.");
                        if (!TryKillUiProcess(process)) {
                            success = false;
                            return;
                        }
                    }
                    HandleUiExited(process);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                // Normal exit or replacement cancels only this process's watchdog.
            }
            catch (Exception ex) {
                ArcLog.GetLogger<UIRunnerService>().Error(ex);
                success = false;
            }
            finally { completion.TrySetResult(success); }
        }

        private static bool TryKillUiProcess(Process process) {
            try {
                if (!process.HasExited) process.Kill();
                if (process.WaitForExit(2000)) return true;
                ArcLog.GetLogger<UIRunnerService>().Error($"UI process {process.Id} did not exit after termination; retaining its tracking.");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<UIRunnerService>().Error(ex);
            }
            return false;
        }

        public void RestartUI() {
            lock (_processLock) {
                if (_isDisposed || App.IsShuttingDown) return;
                if (_processUI != null) {
                    try {
                        if (!_processUI.HasExited && (!_processUI.Responding || !_processUI.CloseMainWindow() || !_processUI.WaitForExit(500))) {
                            if (!TryKillUiProcess(_processUI)) return;
                        }
                        int pid = _processUI.Id;
                        ReleaseProcess();
                        _jobService.StopPlugin(pid);
                    }
                    catch (Exception e) {
                        ArcLog.GetLogger<UIRunnerService>().Error(e);
                        return;
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
                        TryKillUiProcess(_processUI);
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
            // Process invokes Exited under its own monitor. Never wait for
            // _processLock here: another lifecycle operation can hold it while
            // Process.Dispose waits for that same monitor.
            if (sender is Process process) {
                ThreadPool.QueueUserWorkItem(_ => HandleUiExited(process));
            }
        }

        // Returns true when a pending update takes over reopening the UI.
        private bool HandleUiExited(Process process) {
            lock (_processLock) {
                // An exit callback may already be queued when RestartUI detaches
                // the handler. It must never clear or stop the replacement process.
                if (!ReferenceEquals(process, _processUI)) return false;

                var pid = process.Id;
                ReleaseProcess();

                if (_isDisposed || App.IsShuttingDown) return false;

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
                        _uiUpdateTask = restartService.ExecutePendingPluginUpdateWithWindowAsync();
                        return true;
                    }
                }
                return false;
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
                                    TryKillUiProcess(_processUI);
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
            _uiExitCancellation?.Cancel();
            _uiExitCancellation?.Dispose();
            _uiExitCancellation = null;
            _uiExitTask = null;
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
        private readonly TimeSpan _uiExitTimeout;
        private CancellationTokenSource? _uiExitCancellation;
        private Task<bool>? _uiExitTask;
        private Task? _uiUpdateTask;
    }
}
