using System.Collections.Concurrent;
using System.Diagnostics;
using NLog;
using Windows.ApplicationModel.Core;

/*
 * 0.6.1.0：效果定制、壁纸整理与轮播，三项功能按完整使用流程设计。
 *
 * 1. 效果预设
 *   提供内置与用户预设，支持保存、应用、重命名、复制、删除及恢复默认效果。
 *   支持跨壁纸复用；按壁纸类型检查适用性，提供效果预览与取消还原。
 *   预设只保存可复用参数，不携带素材路径、深度图或显示器标识；不覆盖内置预设。
 *
 * 2. 壁纸整理
 *   支持收藏、标签筛选，以及覆盖标题、描述、作者、标签的全库搜索。
 *   搜索、筛选与分页加载保持一致；完善排序和批量整理操作。
 *   用户整理信息独立保存，重启后保留，内置壁纸更新不覆盖用户的收藏与分类。
 *
 * 3. 壁纸轮播
 *   支持分组/播放列表管理、顺序与随机播放、切换间隔、上一张/下一张、暂停/继续。
 *   支持多屏独立列表及复制/扩展布局，明确每个列表的显示目标。
 *   手动应用壁纸时暂停对应目标的轮播；每张壁纸沿用自身效果设置。
 *   与全屏、节能等播放规则联动，暂停时停止计时；唤醒后不补执行错过的切换。
 *   缺失或播放失败的条目可跳过并反馈，全部不可用时暂停，避免无限重试。
 *   保存列表、目标、播放方式和进度；处理重启、壁纸删除、目录迁移及显示器变化。
 *
 * 验收：旧配置与壁纸兼容、状态持久化、失败/取消时保留数据；补充单元及真实 UI 测试。
 *   重点覆盖预设跨类型应用、全库筛选分页、多屏轮播与播放策略的组合行为。
 *
 * 后续需求（不纳入 0.6.1.0）：
 * 音乐桌面壁纸：先做音乐律动、歌曲信息卡片及播放控制。
 *   宿主统一采集和分析输出音频，读取支持的系统媒体会话，再分发给各屏壁纸。
 *   后续评估本地 LRC 歌词；在线歌词、乐评、曲库及自带播放器另行确定范围。
 *
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
