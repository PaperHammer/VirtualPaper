using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Templates {
    public abstract class ArcPage : Page {
        public abstract ArcPageContext Context { get; }
        public abstract Type PageType { get; }
        /// <summary>
        /// 页面是否在导航后仍保持在内存中继续运行
        /// </summary>
        public virtual bool KeepAlive => false;

        protected ArcPage() {
            this.Loaded += ArcPage_Loaded;
            this.Unloaded += ArcPage_Unloaded;
        }

        #region life cycle
        private void ArcPage_Loaded(object sender, RoutedEventArgs e) {
            EnsureContextRegistered();
        }

        private void ArcPage_Unloaded(object sender, RoutedEventArgs e) {
            UnregisterContext();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            EnsureContextRegistered();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e) {
            base.OnNavigatedFrom(e);
            UnregisterContext();
        }
        #endregion

        #region utils
        private void EnsureContextRegistered() {
            Context.IsActive = true;
            if (!PageContextManager.HasContext(PageType)) {
                PageContextManager.RegisterContext(PageType, Context!);
            }
        }

        private void UnregisterContext() {
            if (Context != null) {
                Context.IsActive = false;
                PageContextManager.UnregisterContext(PageType);
            }
        }
        #endregion
    }
}
