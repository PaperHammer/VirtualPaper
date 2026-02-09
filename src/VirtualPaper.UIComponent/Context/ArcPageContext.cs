using System;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.TaskUtils;
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
        public ArcLoadingContext? LoadingContext => _loadingContext;

        /// <summary>
        /// 是否处于活动状态
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// 检查上下文是否有效
        /// </summary>
        public bool IsValid => PageInstance != null && (LoadingContext?.IsValid ?? true);

        public TaskBlocking KeepAliveBlocking => _keepAliveBlocking;

        public ArcPageContext(Page pageInstance) {
            _pageReference = new WeakReference<Page>(pageInstance);
        }

        public ArcPageContext(Page pageInstance, Loading loadingControl) : this(pageInstance) {
            _loadingContext = new ArcLoadingContext(this, loadingControl);
        }

        private readonly WeakReference<Page> _pageReference;
        private readonly ArcLoadingContext? _loadingContext;
        private readonly TaskBlocking _keepAliveBlocking = new();
    }
}