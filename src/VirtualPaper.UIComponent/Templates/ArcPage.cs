using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Templates {
    public abstract class ArcPage : Page {
        public abstract ArcPageHost PageHost { get; }
        public abstract ArcPageContext Context { get; }
        public abstract Type PageType { get; }

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
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e) {
            base.OnNavigatedFrom(e);
        }
        #endregion

        #region utils
        private void EnsureContextRegistered() {
            if (!PageContextManager.HasContext(PageType)) {
                PageContextManager.RegisterContext(PageType, Context!);
            }
        }

        private void UnregisterContext() {
            if (Context != null) {
                PageContextManager.UnregisterContext(PageType);
            }
        }
        #endregion
    }
}
