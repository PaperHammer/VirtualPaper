using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Templates;

namespace VirtualPaper.UIComponent.Utils {
    /// <summary>
    /// 全局消息服务
    /// </summary>
    public static class GlobalMessageUtil {
        private static void AddMsg(ArcWindow? arcWindow, GlobalMsgInfo globalMsgInfo, bool isAllowDuplication = false) {
            arcWindow ??= ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main))!;
            ExecuteOnUIThread(() => {
                // 如果key不为null且不允许重复，检查是否已存在
                if (globalMsgInfo.Key != null &&
                    !isAllowDuplication &&
                    GetGlobalMsg(globalMsgInfo.Key, arcWindow) != null)
                    return;

                // 如果key为null，生成唯一key（时间戳+随机数）
                if (globalMsgInfo.Key == null) {
                    globalMsgInfo.SetUniqueKey(GenerateUniqueKey());
                }

                globalMsgInfo.PropertyChanged += (sender, args) => {
                    if (args.PropertyName == nameof(GlobalMsgInfo.IsOpen)) {
                        if (sender is GlobalMsgInfo msg && !msg.IsOpen) {
                            RemoveMsg(msg, arcWindow);
                        }
                    }
                };

                if (Constants.IsNormalRun) {
                    arcWindow.InfobarMessages.Add(globalMsgInfo);
                }
            });
        }

        public static void CloseAndRemoveMsg(ArcWindow arcWindow, string key) {
            ExecuteOnUIThread(() => {
                if (key == null) return;

                var msg = GetGlobalMsg(key, arcWindow);
                if (msg != null) {
                    msg.IsOpen = false;
                }
            });
        }

        /// <summary>
        /// 显示信息消息
        /// </summary>
        public static void ShowInfo(string message, ArcWindow? arcWindow = null, string? key = null, bool isNeedLocalizer = false, string? extraMsg = null) {
            AddMsg(arcWindow, new GlobalMsgInfo(key, isNeedLocalizer, message, extraMsg, InfoBarSeverity.Informational));
        }

        /// <summary>
        /// 显示成功消息
        /// </summary>
        public static void ShowSuccess(string message, ArcWindow? arcWindow = null, string? key = null, bool isNeedLocalizer = false, string? extraMsg = null) {
            AddMsg(arcWindow, new GlobalMsgInfo(key, isNeedLocalizer, message, extraMsg, InfoBarSeverity.Success));
        }

        /// <summary>
        /// 显示警告消息
        /// </summary>
        public static void ShowWarning(string message, ArcWindow? arcWindow = null, string? key = null, bool isNeedLocalizer = false, string? extraMsg = null) {
            AddMsg(arcWindow, new GlobalMsgInfo(key, isNeedLocalizer, message, extraMsg, InfoBarSeverity.Warning));
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        public static void ShowError(string message, ArcWindow? arcWindow = null, string? key = null, bool isNeedLocalizer = false, string? extraMsg = null) {
            AddMsg(arcWindow, new GlobalMsgInfo(key, isNeedLocalizer, message, extraMsg, InfoBarSeverity.Error));
        }

        /// <summary>
        /// 显示异常信息
        /// </summary>
        public static void ShowException(Exception ex, ArcWindow? arcWindow = null, string? key = null, bool isNeedLocalizer = false, string? extraMsg = null) {
            var message = isNeedLocalizer ? LanguageUtil.GetI18n(ex.Message) : ex.Message;
            AddMsg(arcWindow, new GlobalMsgInfo(key, false, message, extraMsg, InfoBarSeverity.Error));
        }

        public static void ShowCanceled(ArcWindow? arcWindow = null) {
            AddMsg(arcWindow, new GlobalMsgInfo(
                key: nameof(Constants.I18n.InfobarMsg_Cancel),
                isNeedLocalizer: true,
                msgOri18nKey: nameof(Constants.I18n.InfobarMsg_Cancel),
                extraMsg: null,
                infoBarSeverity: InfoBarSeverity.Informational));
        }

        /// <summary>
        /// 清除所有消息
        /// </summary>
        public static void ClearAll(ArcWindow? arcWindow = null) {
            arcWindow ??= ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main))!;
            ExecuteOnUIThread(() => {
                foreach (var msg in arcWindow.InfobarMessages.ToList()) {
                    msg.IsOpen = false;
                }
                arcWindow.InfobarMessages.Clear();
            });
        }

        /// <summary>
        /// 检查是否存在指定key的消息
        /// </summary>
        public static bool ContainsKey(ArcWindow arcWindow, string key) {
            if (key == null) return false;
            return GetGlobalMsg(key, arcWindow) != null;
        }

        /// <summary>
        /// 显示自动关闭的消息
        /// </summary>
        public static void ShowAutoCloseMessage(string message, InfoBarSeverity severity, int autoCloseDelay = 5000, string? key = null, ArcWindow? arcWindow = null) {
            var msgInfo = new GlobalMsgInfo(key, false, message, null, severity);
            AddMsg(arcWindow, msgInfo);

            System.Threading.Tasks.Task.Delay(autoCloseDelay).ContinueWith(t => {
                if (msgInfo.Key != null) {
                    CloseAndRemoveMsg(arcWindow ?? ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main))!, msgInfo.Key);
                }
            }, System.Threading.Tasks.TaskScheduler.Default);
        }

        /// <summary>
        /// 更新现有消息内容
        /// </summary>
        public static void UpdateMessage(string key, string newMessage, bool isNeedLocalizer = false, ArcWindow? arcWindow = null) {
            if (key == null) return;

            ExecuteOnUIThread(() => {
                var msg = GetGlobalMsg(key, arcWindow);
                if (msg != null) {
                    msg.Message = isNeedLocalizer ? LanguageUtil.GetI18n(newMessage) : newMessage;
                }
            });
        }

        /// <summary>
        /// 批量添加消息
        /// </summary>
        public static void AddMessages(ArcWindow? arcWindow = null, params (string message, InfoBarSeverity severity, string key)[] messages) {
            foreach (var (message, severity, key) in messages) {
                AddMsg(arcWindow, new GlobalMsgInfo(key, false, message, null, severity));
            }
        }

        /// <summary>
        /// 获取指定key的消息（key为null时返回null）
        /// </summary>
        private static GlobalMsgInfo? GetGlobalMsg(string key, ArcWindow? arcWindow = null) {
            if (key == null) return null;
            arcWindow ??= ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main))!;
            return arcWindow.InfobarMessages.FirstOrDefault(m => m.Key == key);
        }

        /// <summary>
        /// 移除消息
        /// </summary>
        private static void RemoveMsg(GlobalMsgInfo msg, ArcWindow? arcWindow = null) {
            arcWindow ??= ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main))!;
            ExecuteOnUIThread(() => {
                arcWindow.InfobarMessages.Remove(msg);
            });
        }

        /// <summary>
        /// 生成唯一key（时间戳+随机数）
        /// </summary>
        private static string GenerateUniqueKey() {
            return $"msg_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        }

        private static void ExecuteOnUIThread(Action action, bool synchronous = false) {
            CrossThreadInvoker.InvokeOnUIThread(action, synchronous);
        }
    }

    /// <summary>
    /// 全局消息信息
    /// </summary>
    public partial class GlobalMsgInfo : ObservableObject {
        private bool _isOpen = false;
        private string? _key;

        public bool IsOpen {
            get => _isOpen;
            set { _isOpen = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 消息键（为null时表示可以重复展示）
        /// </summary>
        public string? Key => _key;

        public string Message { get; set; }
        public InfoBarSeverity Severity { get; set; }

        public GlobalMsgInfo(string? key, bool isNeedLocalizer, string msgOri18nKey, string? extraMsg, InfoBarSeverity infoBarSeverity) {
            _key = key;
            Severity = infoBarSeverity;

            var mainMessage = isNeedLocalizer ? LanguageUtil.GetI18n(msgOri18nKey) : msgOri18nKey;
            Message = extraMsg != null ? mainMessage + extraMsg : mainMessage;

            IsOpen = true;
        }

        /// <summary>
        /// 设置唯一key（用于key为null时生成唯一标识）
        /// </summary>
        internal void SetUniqueKey(string uniqueKey) {
            _key = uniqueKey;
        }
    }
}