using System.Diagnostics;
using System.IO;
using Octokit;
using VirtualPaper.Common;

namespace VirtualPaper.Utils {
    public static class ProcWarmUpUtil {
        /// <summary>
        /// 注册一个异步预热步骤
        /// </summary>
        public static void Register(Func<Task> action) {
            _actions.Add(action);
        }

        /// <summary>
        /// 注册一个同步预热步骤
        /// </summary>
        public static void Register(Action action) {
            _actions.Add(() => Task.Run(action));
        }

        /// <summary>
        /// 注册一个“尝试启动 EXE 的预热步骤”
        /// </summary>
        public static void RegisterProcessWarmUp(string moduleWorkingDir, string moduleName, string? arguments = null) {
            Register(async () => {
                string workingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, moduleWorkingDir);
                string exePath = Path.Combine(workingDir, moduleName);

                if (!File.Exists(exePath)) {
                    string err = $"Warm-up file not found: {exePath}";
                    App.Log.Error(err);
#if DEBUG
                    Debug.WriteLine(err);
#endif
                    return;
                }

                var start = new ProcessStartInfo {
                    FileName = exePath,
                    WorkingDirectory = workingDir,
                    Arguments = arguments ?? ProcRun.WarmUp.ToString(),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                using var process = new Process {
                    EnableRaisingEvents = false,
                    StartInfo = start
                };

                try {
                    process.Start();
                }
                catch (Exception ex) {
                    App.Log.Error($"Warm-up start failed: {exePath}", ex);
                }

                var readTask = Task.Run(async () => {
                    try {
                        string? line;
                        while ((line = await process.StandardOutput.ReadLineAsync()) != null) {
                            string response = $"[WarmUp:{moduleName} {line}]";
                            App.Log.Info(response);
#if DEBUG
                            Debug.WriteLine(response);
#endif
                        }
                    }
                    catch { }
                });

                // 超时等待 10s
                await Task.Delay(10000);

                if (!process.HasExited) {
                    try {
                        process.Kill(true);
                    }
                    catch {
                        // 忽略，不需要强制 kill
                    }
                }
            });
        }

        /// <summary>
        /// 执行全部预热步骤（仅执行一次）
        /// </summary>
        public static void Run() {
            if (_initialized)
                return;

            _initialized = true;

            foreach (var step in _actions) {
                try {
                    _ = step();
                }
                catch (Exception ex) {
                    App.Log.Error(ex);
#if DEBUG                   
                    System.Diagnostics.Debug.WriteLine($"WarmUp error: {ex}");
#endif
                }
            }
        }

        private static readonly List<Func<Task>> _actions = [];
        private static bool _initialized;
    }
}
