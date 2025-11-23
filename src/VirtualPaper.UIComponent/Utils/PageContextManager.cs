using System;
using System.Collections.Concurrent;
using System.Linq;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;

namespace VirtualPaper.UIComponent.Utils {
    /// <summary>
    /// 页面上下文管理器 - 支持缓存页面管理
    /// </summary>
    public static class PageContextManager {
        private static readonly ConcurrentDictionary<string, ArcPageContext> _contexts = new();

        public static void RegisterContext(Type pageType, ArcPageContext context) {
            ArgumentNullException.ThrowIfNull(pageType);
            ArgumentNullException.ThrowIfNull(context);

            _contexts[pageType.FullName!] = context;
        }

        public static void UnregisterContext(Type pageType) {
            if (pageType == null) return;

            _contexts.TryRemove(pageType.FullName!, out _);
        }

        public static ArcPageContext? GetContext(Type pageType) {
            if (_contexts.TryGetValue(pageType.FullName!, out var context) && context.IsValid) {
                return context;
            }
            return null;
        }

        public static ArcPageContext? GetContext<T>() where T : ArcPage {
            return GetContext(typeof(T));
        }

        public static ArcLoadingContext? GetLoadingContext(Type pageType) {
            return GetContext(pageType)?.LoadingContext;
        }

        public static ArcLoadingContext? GetLoadingContext<T>() where T : ArcPage {
            return GetContext<T>()?.LoadingContext;
        }

        /// <summary>
        /// 检查指定页面类型是否已注册上下文
        /// </summary>
        public static bool HasContext(Type pageType) {
            var key = pageType.FullName!;
            return _contexts.ContainsKey(key) && _contexts[key].IsValid;
        }

        /// <summary>
        /// 获取当前活动的页面上下文
        /// </summary>
        public static ArcPageContext? GetActiveContext() {
            return _contexts.Values.FirstOrDefault(context =>
                context.IsValid && context.IsActive);
        }

        /// <summary>
        /// 设置页面活动状态
        /// </summary>
        public static void SetPageActiveState(Type pageType, bool isActive) {
            var context = GetContext(pageType);
            if (context != null) {
                context.IsActive = isActive;
            }
        }
    }
}
