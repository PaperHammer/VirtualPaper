using System;
using System.Collections.Concurrent;
using NLog;
using Windows.ApplicationModel.Core;

namespace VirtualPaper.UIComponent.Logging {
    /// <summary>
    /// 统一日志入口：支持程序集自动识别、调试输出、缓存
    /// </summary>
    public static class ArcLog {
        /// <summary>
        /// 获取指定类型的日志记录器（自动识别程序集）
        /// </summary>
        public static ArcLoggerProxy GetLogger<T>() {
            var type = typeof(T);
            var assemblyName = type.Assembly.GetName().Name ?? "UnknownAssembly";
            var loggerName = $"{assemblyName}.{type.FullName}";
            return _cache.GetOrAdd(loggerName, _ =>
                new ArcLoggerProxy(LogManager.GetLogger(loggerName), assemblyName));
        }

        private static readonly ConcurrentDictionary<string, ArcLoggerProxy> _cache = new();
    }

    /// <summary>
    /// 日志代理：包装 NLog 并在 Debug 模式输出控制台信息
    /// </summary>
    public sealed class ArcLoggerProxy {
        private readonly Logger _inner;
        private readonly string _assemblyName;

        internal ArcLoggerProxy(Logger inner, string assemblyName) {
            _inner = inner;
            _assemblyName = assemblyName;
        }

        private void WriteDebugLine(string level, string? message) {
            System.Diagnostics.Debug.WriteLine($"[{level}] ({_assemblyName}) {_inner.Name}: {message}");
        }

        public void Info(string message) {
            WriteDebugLine("INFO", message);
            _inner.Info(message);
        }

        public void Debug(string message) {
            WriteDebugLine("DEBUG", message);
            _inner.Debug(message);
        }

        public void Warn(string message) {
            WriteDebugLine("WARN", message);
            _inner.Warn(message);
        }

        public void Error(string message, Exception? ex = null) {
            WriteDebugLine("ERROR", message);
            if (ex != null)
                System.Diagnostics.Debug.WriteLine(ex);
            _inner.Error(ex, message);
        }

        public void Error(Exception ex) {
            WriteDebugLine("ERROR", ex.ToString());
            _inner.Error(ex);
        }
        
        public void Error(UnhandledError ex) {
            WriteDebugLine("ERROR", ex.ToString());
            _inner.Error(ex);
        }

        public void Fatal(string message) {
            WriteDebugLine("FATAL", message);
            _inner.Fatal(message);
        }

        public void Trace(string message) {
            WriteDebugLine("TRACE", message);
            _inner.Trace(message);
        }
    }
}
