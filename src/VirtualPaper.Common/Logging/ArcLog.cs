using System.Collections.Concurrent;
using System.Diagnostics;
using NLog;
using Windows.ApplicationModel.Core;

namespace VirtualPaper.Common.Logging {
    /// <summary>
    /// 统一日志入口：支持程序集自动识别、调试输出、缓存
    /// </summary>
    public static class ArcLog {
        /// <summary>
        /// 获取指定类型的日志记录器（自动识别程序集）
        /// </summary>
        public static ArcLoggerProxy GetLogger<T>() {
            var type = typeof(T);
            var loggerName = type.FullName ?? "UnknownTypeFullName";
            return _cache.GetOrAdd(loggerName, _ =>
                new ArcLoggerProxy(LogManager.GetLogger(loggerName)));
        }

        private static readonly ConcurrentDictionary<string, ArcLoggerProxy> _cache = new();
    }

    /// <summary>
    /// 日志代理：包装 NLog 并在 Debug 模式输出控制台信息
    /// </summary>
    public sealed class ArcLoggerProxy {
        private readonly Logger _inner;

        internal ArcLoggerProxy(Logger inner) {
            _inner = inner;
        }

        [Conditional("DEBUG")]
        private void WriteDebugLine(string level, string? message) {
            Console.WriteLine($"[{level}] ({_inner.Name}): {message}");
            System.Diagnostics.Debug.WriteLine($"[{level}] ({_inner.Name}): {message}");
        }

        public void Info(string message) {
#if DEBUG
            WriteDebugLine("INFO", message);
#endif
            _inner.Info(message);
        }

        [Conditional("DEBUG")]
        public void Debug(string message) {
#if DEBUG
            WriteDebugLine("DEBUG", message);
#endif
            _inner.Debug(message);
        }

        public void Warn(string message) {
#if DEBUG
            WriteDebugLine("WARN", message);
#endif
            _inner.Warn(message);
        }

        public void Error(string message, Exception? ex = null) {
#if DEBUG
            WriteDebugLine("ERROR", message);
            if (ex != null)
                System.Diagnostics.Debug.WriteLine(ex);
#endif
            _inner.Error(ex, message);
        }

        public void Error(Exception ex) {
#if DEBUG
            WriteDebugLine("ERROR", ex.ToString());
#endif
            _inner.Error(ex);
        }
        
        public void Error(UnhandledError ex) {
#if DEBUG
            WriteDebugLine("ERROR", ex.ToString());
#endif
            _inner.Error(ex);
        }

        public void Fatal(string message) {
#if DEBUG
            WriteDebugLine("FATAL", message);
#endif
            _inner.Fatal(message);
        }

        public void Trace(string message) {
#if DEBUG
            WriteDebugLine("TRACE", message);
#endif
            _inner.Trace(message);
        }
    }
}
