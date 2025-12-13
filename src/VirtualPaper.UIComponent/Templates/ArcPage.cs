using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;

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
        /// <summary>
        /// 该类型是否会存在多个实例（同类型多实例无法使用 ArcPageContext 管理器）
        /// </summary>
        protected virtual bool IsMultiInstance => false;

        protected ArcPage() {
            this.Unloaded += ArcPage_Unloaded;
        }

        protected virtual void ArcPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            _ = OnDestroyAsync();
        }

        #region async life-cycle hooks
        public void NavigateEnter(NavigationPayload? parameter) {
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
        protected virtual Task OnEnterAsync(NavigationPayload? parameter) {
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
            if (Context is null)
                return;

            Context.IsActive = true;

            var key = GetContextKey();

            if (!ArcPageContextManager.HasContext(key)) {
                ArcPageContextManager.RegisterContext(key, Context);
            }
        }

        private void UnregisterContext() {
            if (Context is null)
                return;

            Context.IsActive = false;

            var key = GetContextKey();
            ArcPageContextManager.UnregisterContext(key);
        }

        /// <summary>
        /// 为单实例 / 多实例生成统一的 ContextKey。
        /// 多实例依赖 TimeSpan，单实例不依赖。
        /// </summary>
        private ArcPageContextKey GetContextKey() {
            return IsMultiInstance
                ? new ArcPageContextKey(PageType, _timeSpan)
                : new ArcPageContextKey(PageType);
        }

        /// <summary>
        /// 获取多实例对应的上下文（单实例返回 null）。
        /// </summary>
        public ArcPageContext? GetContextForMultiInstance() {
            if (!IsMultiInstance) return null;

            var key = new ArcPageContextKey(PageType, _timeSpan);
            return ArcPageContextManager.GetContext(key);
        }
        #endregion

        private int _isPreLeaved;
        private readonly long _timeSpan = DateTime.UtcNow.Ticks;
    }
}
