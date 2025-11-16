using System;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Feedback;

namespace VirtualPaper.UIComponent.Context {
    public record ArcPageContext {
        /// <summary>
        /// 页面实例
        /// </summary>
        public Page? PageInstance => _pageReference.TryGetTarget(out var page) ? page : null;

        /// <summary>
        /// 加载上下文（直接暴露供外部调用）
        /// </summary>
        public LoadingContext? Loading => _loadingContext;

        /// <summary>
        /// 是否处于活动状态
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// 检查上下文是否有效
        /// </summary>
        public bool IsValid => PageInstance != null && (Loading?.IsValid ?? true);

        public ArcPageContext(Page pageInstance) {
            _pageReference = new WeakReference<Page>(pageInstance);
        }

        public ArcPageContext(Page pageInstance, Loading loadingControl) {
            _pageReference = new WeakReference<Page>(pageInstance);
            _loadingContext = new LoadingContext(loadingControl);
        }

        private readonly WeakReference<Page> _pageReference;
        private readonly LoadingContext? _loadingContext;
    }
}