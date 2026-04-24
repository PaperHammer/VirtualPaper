using System.Collections.Concurrent;
using System.Diagnostics;
using NLog;
using Windows.ApplicationModel.Core;

/*
 * todo
 * 时间感知	日出日落自动切换壁纸亮度/色调
 * AI 生成壁纸	接入 Stable Diffusion / ComfyUI，本地生成动态壁纸
 * AI 实时风格化	对摄像头画面实时风格迁移作为壁纸
 */

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
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] ({_inner.Name}): {message}");
        }

        public void Info(string message) {
            WriteDebugLine("INFO", message);
            _inner.Info(message);
        }

        [Conditional("DEBUG")]
        public void Debug(string message) {
            WriteDebugLine("DEBUG", message);
            _inner.Debug(message);
        }

        public void Warn(string message) {
            WriteDebugLine("WARN", message);
            _inner.Warn(message);
        }

        public void Error(string message, Exception? ex = null) {
            WriteDebugLine("ERROR", $"{message}\n\t{ex}");
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
