using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.TaskUtils;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Templates {
    public abstract class ArcPage : Page {
        public abstract ArcPageContext Context { get; }
        public abstract Type PageType { get; }
        /// <summary>
        /// 页面是否保活
        /// </summary>
        public bool KeepAlive => GetType().GetCustomAttribute<KeepAliveAttribute>()?.Value == true;
        public new Type GetType() => PageType;
        public bool IsPreLeaved => Volatile.Read(ref _isPreLeaved) == 1;

        #region async life-cycle hooks
        public void NavigateEnter(object? parameter) {
            Volatile.Write(ref _isPreLeaved, 0);
            _ = OnEnterAsync(parameter);
        }

        public async void NavigateExit(Action? beforeLeaveCallback = null) {
            await OnPreLeaveAsync();

            beforeLeaveCallback?.Invoke();

            await OnLeaveAsync();
            await OnDestroyAsync();
        }

        /// <summary>
        /// 页面进入（Loaded 或导航到本页面时）
        /// </summary>
        protected virtual Task OnEnterAsync(object? parameter) {
            EnsureContextRegistered();

            return Task.CompletedTask;
        }

        protected virtual async Task<bool> OnPreLeaveAsync() {
            await Context.Blocking.WaitUntilAllReleasedAsync();

            // 避免 JIT 优化代码顺序
            Volatile.Write(ref _isPreLeaved, 1);
            return await Task.FromResult(true);
        }

        /// <summary>
        /// 页面离开（导航到其他页面时）
        /// </summary>
        protected virtual Task OnLeaveAsync() {
            if (!KeepAlive) {
                UnregisterContext();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 页面销毁
        /// </summary>
        protected virtual Task OnDestroyAsync(bool force = false) {
            if (force || !KeepAlive) {
                UnregisterContext();
            }

            return Task.CompletedTask;
        }
        #endregion

        #region utils
        private void EnsureContextRegistered() {
            if (Context != null) {
                Context.IsActive = true;
                if (!PageContextManager.HasContext(PageType)) {
                    PageContextManager.RegisterContext(PageType, Context);
                }
            }
        }

        private void UnregisterContext() {
            if (Context == null) return;

            Context.IsActive = false;
            PageContextManager.UnregisterContext(PageType);
        }
        #endregion

        private int _isPreLeaved;
    }
}
