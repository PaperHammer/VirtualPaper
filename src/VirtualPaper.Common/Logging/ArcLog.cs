using System.Collections.Concurrent;
using System.Diagnostics;
using NLog;
using Windows.ApplicationModel.Core;

/*
 * 
 * 一键氛围动态壁纸（优先）：选图 → 选预设 → 预览 → 应用。
 *   复用本地 Web / Canvas，提供星尘、飘雪、萤火、流星及强度设置，无需新增 AI 模型。
 *   保留原画和人物位置；补齐暂停恢复、多屏缩放及 1K / 2K 性能验证。
 * 增强已有 3D 视差：强度预设、平滑鼠标跟随、离开归位及可关闭的缓慢自动漂移。
 *   与氛围效果独立开关；限制边缘拉伸和资源占用，作为已有能力升级。
 * 一键高清适屏：选图 → 选显示器 → 预览适配范围 → 按需超分 → 应用。
 *   复用超分、裁剪和入库能力；保持比例，支持完整显示、裁剪填满及背景填充。
 * 音乐桌面壁纸（下一阶段）：先做音乐律动、歌曲信息卡片及播放控制。
 *   宿主统一采集和分析输出音频，读取支持的系统媒体会话，再分发给各屏壁纸。
 *   后续评估本地 LRC 歌词；在线歌词、乐评、曲库及自带播放器另行确定范围。
 *
 * 分组循环播放
 * AI 生成壁纸	接入 Stable Diffusion / ComfyUI，本地生成动态壁纸
 * 
 * diff-update 二期
 */

namespace VirtualPaper.Common.Logging {
    /// <summary>
    /// 统一日志入口：支持程序集自动识别、调试输出、缓存
    /// </summary>
    public static class ArcLog {
        /// <summary>
        /// 获取指定类型的日志记录器（自动识别程序集）
        /// </summary>
        public static ArcLoggerProxy GetLogger<T>() => GetLogger(typeof(T));

        public static ArcLoggerProxy GetLogger(Type type) {
            ArgumentNullException.ThrowIfNull(type);

            string loggerName = type.FullName ?? "UnknownTypeFullName";
            return _cache.GetOrAdd(
                loggerName,
                _ => new ArcLoggerProxy(LogManager.GetLogger(loggerName)));
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
