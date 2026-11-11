using System.Collections.Concurrent;
using System.Diagnostics;
using NLog;
using Windows.ApplicationModel.Core;

/*
 * 0.6.1.0 待办：壁纸轮播与 MSIX/EXE 双渠道的全面多语言适配。
 *
 * 1. 壁纸轮播
 *   支持分组/播放列表管理、顺序与随机播放、切换间隔、上一张/下一张、暂停/继续。
 *   支持多屏独立列表及复制/扩展布局，明确每个列表的显示目标。
 *   手动应用壁纸时暂停对应目标的轮播；每张壁纸沿用自身效果设置。
 *   与全屏、节能等播放规则联动，暂停时停止计时；唤醒后不补执行错过的切换。
 *   缺失或播放失败的条目可跳过并反馈，全部不可用时暂停，避免无限重试。
 *   保存列表、目标、播放方式和进度；处理重启、壁纸删除、目录迁移及显示器变化。
 *
 * 2. 全面多语言适配（MSIX 与 EXE）
 *   两个渠道共用支持语言清单、源文案及资源生成/校验链路，以现有中英资源为基础扩展目标语种。
 *   覆盖主界面、托盘、图像/Web 编辑器、AI 页面、播放器、菜单、弹窗及用户提示，清理硬编码文案。
 *   统一系统/区域语言匹配与英文回退，持久化用户选择，完善跨窗口热切换及缺失资源回退。
 *   检查长文本、字体回退、日期/数字格式和适用语种的书写方向，保证布局与交互可用。
 *   EXE 安装/卸载向导同步本地化，安装语言仅在首次初始化应用；MSIX 首次运行匹配系统首选语言。
 *   两个渠道升级均保留用户语言并刷新随包资源；校验两种安装包的支持语种资源完整可用及 MSIX 语言声明。
 *   商店详情、更新说明和截图按语种准备；分别验证 MSIX/EXE 的安装、首次运行、升级和语言热切换。
 *
 * 验收：旧配置与壁纸兼容、状态持久化、失败/取消时保留数据；补充单元及真实 UI 测试。
 *   重点覆盖多屏轮播与播放策略、语言资源及打包声明的一致性。
 *   壁纸整理、效果预设的真实 UI 验收待用户授权后补齐。
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
