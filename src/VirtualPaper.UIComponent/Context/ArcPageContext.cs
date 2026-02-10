using System;
using VirtualPaper.Common.Utils.TaskUtils;
using VirtualPaper.UIComponent.Feedback;
using VirtualPaper.UIComponent.Templates;

namespace VirtualPaper.UIComponent.Context {
    public record ArcPageContext {
        /// <summary>
        /// 页面实例
        /// </summary>
        public ArcPage PageInstance => _pageReference.TryGetTarget(out var page) ? page : throw new NullReferenceException();

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

        public ArcPageContext(ArcPage pageInstance) {
            _pageReference = new WeakReference<ArcPage>(pageInstance);
        }

        public ArcPageContext(ArcPage pageInstance, Loading loadingControl) : this(pageInstance) {
            _loadingContext = new ArcLoadingContext(this, loadingControl);
        }

        public T GetPageInstance<T>() where T : ArcPage {
            return (T)PageInstance;
        }

        private readonly WeakReference<ArcPage> _pageReference;
        private readonly ArcLoadingContext? _loadingContext;
        private readonly TaskBlocking _keepAliveBlocking = new();
    }
}