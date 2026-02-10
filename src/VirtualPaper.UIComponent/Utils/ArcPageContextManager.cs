using System;
using System.Collections.Concurrent;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;

namespace VirtualPaper.UIComponent.Utils {
    public readonly record struct ArcPageContextKey(Type PageType, long? TimeSpan = null);

    /// <summary>
    /// 页面上下文管理器 - 支持缓存页面管理
    /// </summary>
    public static class ArcPageContextManager {
        private static readonly ConcurrentDictionary<ArcPageContextKey, ArcPageContext> _contexts = new();

        public static void RegisterContext(ArcPageContextKey key, ArcPageContext context) {
            ArgumentNullException.ThrowIfNull(context);
            _contexts[key] = context;
        }

        public static void UnregisterContext(ArcPageContextKey key) {
            _contexts.TryRemove(key, out _);
        }

        public static ArcPageContext? GetContext(ArcPageContextKey key) {
            if (_contexts.TryGetValue(key, out var context) && context.IsValid) {
                return context;
            }
            return null;
        }

        public static ArcPageContext? GetContext<T>() where T : ArcPage {
            return GetContext(typeof(T));
        }

        public static ArcPageContext? GetContext(Type pageType) {
            ArgumentNullException.ThrowIfNull(pageType);
            return GetContext(new ArcPageContextKey(pageType));
        }

        public static ArcLoadingContext? GetLoadingContext(ArcPageContextKey key) {
            return GetContext(key)?.LoadingContext;
        }

        public static ArcLoadingContext? GetLoadingContext<T>() where T : ArcPage {
            return GetContext<T>()?.LoadingContext;
        }

        /// <summary>
        /// 检查指定页面类型是否已注册上下文
        /// </summary>
        public static bool HasContext(ArcPageContextKey key) {
            return _contexts.ContainsKey(key) && _contexts[key].IsValid;
        }
    }
}
