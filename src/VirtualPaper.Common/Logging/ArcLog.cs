using System.Collections.Concurrent;
using System.Diagnostics;
using NLog;
using Windows.ApplicationModel.Core;

/*
 * 0.6.1.0 待办：多语言实机验收、MSIX/EXE 双渠道验收与商店素材。
 *
 * 多语言发布验收（MSIX 与 EXE）
 *   实机检查长文本布局、字体回退及日期/数字格式。
 *   分别验证两种安装包安装后的语言资源、首次运行、升级和跨窗口语言热切换。
 *   准备各语种的商店详情、更新说明和截图。
 *
 * 壁纸整理、效果预设：真实 UI 验收待用户授权后执行。
 *
 * 硬件加速 配置项
 *   根据硬件环境，模型多精度
 *
 * AI 生成壁纸	接入 Stable Diffusion / ComfyUI
 *
 * 根目录清理
 *
 * 壁纸轮播
 *   支持分组/播放列表管理、顺序与随机播放、切换间隔、上一张/下一张、暂停/继续。
 *   支持多屏独立列表及复制/扩展布局，明确每个列表的显示目标。
 *   手动应用壁纸时暂停对应目标的轮播；每张壁纸沿用自身效果设置。
 *   与全屏、节能等播放规则联动，暂停时停止计时；唤醒后不补执行错过的切换。
 *   缺失或播放失败的条目可跳过并反馈，全部不可用时暂停，避免无限重试。
 *   保存列表、目标、播放方式和进度；处理重启、壁纸删除、目录迁移及显示器变化。
 *
 * 后续需求：
 *
 * 音乐桌面壁纸：先做音乐律动、歌曲信息卡片及播放控制。
 *   宿主统一采集和分析输出音频，读取支持的系统媒体会话，再分发给各屏壁纸。
 *   后续评估本地 LRC 歌词；在线歌词、乐评、曲库及自带播放器另行确定范围。
 *
 * diff-update 二期
 *
 * 外部 API 支持
 *   支持外部 API 调用，如天气、日历、RSS 等。
 *   支持自定义 API 调用，如天气、日历、RSS 等。
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
