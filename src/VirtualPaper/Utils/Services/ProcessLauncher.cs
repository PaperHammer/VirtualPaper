using System.Diagnostics;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Utils.Services {
    public class ProcessLauncher : IProcessLauncher, IDisposable {
        public event EventHandler? Exited;
        public event EventHandler<ProcessOutputEventArgs>? OutputDataReceived;

        public bool HasExited => _process?.HasExited ?? true;
        public int ProcessId {
            get {
                EnsureProcessStarted();
                return _process!.Id;
            }
        }

        public void Launch(ProcessStartInfo startInfo) {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _process = new Process {
                EnableRaisingEvents = true,
                StartInfo = startInfo,
            };

            // 桥接 Process 原生事件 → 自定义事件
            _process.Exited += OnExited;
            _process.OutputDataReceived += OnOutputDataReceived;

            _process.Start();
        }

        public void BeginOutputReadLine() {
            EnsureProcessStarted();
            _process!.BeginOutputReadLine();
        }

        public void WriteStdin(string msg) {
            EnsureProcessStarted();
            try {
                _process!.StandardInput.WriteLine(msg);
            }
            catch (Exception e) {
                // 保持与原代码一致的日志风格，调用方可以选择捕获或忽略
                throw new InvalidOperationException($"Stdin write fail: {e.Message}", e);
            }
        }

        public void Kill() {
            try {
                if (_process != null && !_process.HasExited) {
                    _process.Kill();
                }
            }
            catch (InvalidOperationException) {
                // 进程已退出，忽略
            }
        }

        // 桥接 Process.Exited → IProcessLauncher.Exited
        private void OnExited(object? sender, EventArgs e) {
            Exited?.Invoke(this, e);
        }

        // 桥接 Process.OutputDataReceived → IProcessLauncher.OutputDataReceived
        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (e.Data != null) {
                OutputDataReceived?.Invoke(this, new ProcessOutputEventArgs(e.Data));
            }
        }

        private void EnsureProcessStarted() {
            if (_process == null)
                throw new InvalidOperationException("Process has not been launched yet. Call Launch() first.");
        }

        #region Dispose

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    if (_process != null) {
                        _process.Exited -= OnExited;
                        _process.OutputDataReceived -= OnOutputDataReceived;
                        _process.Dispose();
                        _process = null;
                    }
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        private Process? _process;
        private bool _isDisposed;
    }
}
