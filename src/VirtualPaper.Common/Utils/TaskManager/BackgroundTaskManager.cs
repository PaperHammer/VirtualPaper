using System.Collections.Concurrent;
using System.Diagnostics;

namespace VirtualPaper.Common.Utils.TaskManager {
    public class BackgroundTaskManager : IDisposable {
        /// <summary>
        /// 启动一个后台任务
        /// </summary>
        /// <param name="parameters">任务参数</param>
        /// <param name="executor">任务执行器</param>
        /// <returns>任务UID</returns>
        public string StartTask(object parameters, IBackgroundTaskExecutor executor) {
            if (_disposed) throw new ObjectDisposedException(nameof(BackgroundTaskManager));
            if (executor == null) throw new ArgumentNullException(nameof(executor));

            var taskId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();

            var task = Task.Run(async () => {
                try {
                    await executor.ExecuteAsync(parameters, cts.Token);
                }
                catch (OperationCanceledException) {
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Background task failed: {ex}");
                }
                finally {
                    _runningTasks.TryRemove(taskId, out _);
                }
            }, cts.Token);

            _runningTasks.TryAdd(taskId, (task, cts));
            return taskId;
        }

        /// <summary>
        /// 停止指定任务
        /// </summary>
        /// <param name="taskId">任务UID</param>
        /// <param name="waitForCompletion">是否等待任务完成</param>
        public void StopTask(string taskId, bool waitForCompletion = false) {
            if (_runningTasks.TryRemove(taskId, out var taskInfo)) {
                taskInfo.Cts.Cancel();
                if (waitForCompletion) {
                    taskInfo.Task.Wait();
                }
            }
        }

        /// <summary>
        /// 停止所有任务
        /// </summary>
        /// <param name="waitForCompletion">是否等待任务完成</param>
        public void StopAllTasks(bool waitForCompletion = false) {
            foreach (var taskId in _runningTasks.Keys) {
                StopTask(taskId, waitForCompletion);
            }
        }

        /// <summary>
        /// 检查任务是否正在运行
        /// </summary>
        public bool IsTaskRunning(string taskId) {
            return _runningTasks.ContainsKey(taskId);
        }

        public void Dispose() {
            if (_disposed) return;

            _disposed = true;
            StopAllTasks(waitForCompletion: true);

            foreach (var taskInfo in _runningTasks.Values) {
                taskInfo.Cts.Dispose();
            }

            _runningTasks.Clear();
            GC.SuppressFinalize(this);
        }

        private readonly ConcurrentDictionary<string, (Task Task, CancellationTokenSource Cts)> _runningTasks = new();
        private bool _disposed;
    }

    /// <summary>
    /// 后台任务执行器接口
    /// </summary>
    public interface IBackgroundTaskExecutor {
        /// <summary>
        /// 执行后台任务
        /// </summary>
        /// <param name="parameters">任务参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task ExecuteAsync(object parameters, CancellationToken cancellationToken);
    }
}
