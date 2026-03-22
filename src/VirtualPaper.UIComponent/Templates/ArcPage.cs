using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Templates {
    public abstract class ArcPage : Page {
        public virtual ArcPageContext ArcContext { get; set; }
        public abstract Type ArcType { get; }
        /// <summary>
        /// 页面是否保活（无法阻止 Unloaded）
        /// </summary>
        public bool KeepAlive => _keepAlive.Value;
        public bool IsPreLeaved => Volatile.Read(ref _isPreLeaved) == 1;
        /// <summary>
        /// 该类型是否会存在多个实例（同类型多实例无法使用 ArcPageContext 管理器）
        /// </summary>
        protected virtual bool IsMultiInstance => false;
        protected ArcPageContextKey ContextKey => GetContextKey();
        public FrameworkPayload? Payload { get; protected set; }
        public ArcPageStatus Status { get; protected set; }

        protected ArcPage() {
            ArcContext = new ArcPageContext(this);
            this.Loaded += ArcPage_Loaded;
            this.Unloaded += ArcPage_Unloaded;
            _keepAlive = new Lazy<bool>(() => ArcType.GetCustomAttribute<KeepAliveAttribute>()?.Value == true);
        }

        private void ArcPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            EnsureContextRegistered();
        }

        protected virtual void ArcPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            OnDestroy();
        }

        #region async life-cycle hooks
        public void NavigateEnter(FrameworkPayload? payload) {
            Volatile.Write(ref _isPreLeaved, 0);
            OnEnter(payload);
        }

        public async void NavigateExit(Action? beforeLeave = null, Action? afterDestoried = null) {
            _exitCts?.Cancel(); //以此防范极其罕见的并发
            _exitCts = new CancellationTokenSource();
            var token = _exitCts.Token;

            try {
                Status = ArcPageStatus.BackgroundRunning;
                await OnPreLeaveAsync();

                if (!KeepAlive) {
                    beforeLeave?.Invoke();

                    if (token.IsCancellationRequested) return;

                    await OnLeaveAsync();
                    OnDestroy();

                    if (token.IsCancellationRequested) return;

                    afterDestoried?.Invoke();
                }
            }
            catch (OperationCanceledException) {
                // 被复活，忽略退出逻辑
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ArcPage>().Error(ex);
            }
            finally {
                if (_exitCts != null && !_exitCts.IsCancellationRequested) {
                    _exitCts.Dispose();
                    _exitCts = null;
                }
            }
        }

        /// <summary>
        /// 页面进入
        /// </summary>
        protected virtual void OnEnter(FrameworkPayload? payload) {
            if (_exitCts != null) {
                _exitCts.Cancel();
                _exitCts.Dispose();
                _exitCts = null;
            }

            CrossThreadInvoker.InvokeOnUIThread(() => {
                Status = ArcPageStatus.PreActive;
                this.Translation = new System.Numerics.Vector3(0, 0, 0);
                this.Opacity = 1;
                this.IsHitTestVisible = true;
                this.Payload = payload;
            });
        }

        protected virtual async Task OnPreLeaveAsync() {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                this.Opacity = 0.0;
                this.IsHitTestVisible = false;
                this.Translation = new System.Numerics.Vector3(0, 10000, 0);
                Canvas.SetZIndex(this, 0);
            });

            if (ArcContext != null) {
                ArcContext.IsActive = false;
                await ArcContext.KeepAliveBlocking.WaitAsync();
            }

            // 避免 JIT 优化代码顺序
            Volatile.Write(ref _isPreLeaved, 1);
        }

        /// <summary>
        /// 页面离开
        /// </summary>
        protected virtual Task OnLeaveAsync() {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 页面销毁
        /// </summary>
        protected virtual void OnDestroy() {
            Status = ArcPageStatus.Stopped;            
            Payload = null;
            DataContext = null;
            UnregisterContext();
            ArcContext = null;
        }
        #endregion

        #region utils
        private void EnsureContextRegistered() {
            if (ArcContext is null)
                return;

            ArcContext.IsActive = true;

            var key = GetContextKey();
            if (!ArcPageContextManager.HasContext(key)) {
                ArcPageContextManager.RegisterContext(key, ArcContext);
            }
        }

        private void UnregisterContext() {
            if (ArcContext is null)
                return;

            ArcContext.IsActive = false;

            var key = GetContextKey();
            ArcPageContextManager.UnregisterContext(key);
        }

        /// <summary>
        /// 为单实例 / 多实例生成统一的 ContextKey。
        /// 多实例依赖 TimeSpan，单实例不依赖。
        /// </summary>
        private ArcPageContextKey GetContextKey() {
            return IsMultiInstance
                ? new ArcPageContextKey(ArcType, _timeSpan)
                : new ArcPageContextKey(ArcType);
        }

        internal void SetActiveStatus() {
            Status = ArcPageStatus.Active;
        }
        #endregion

        private int _isPreLeaved;
        private readonly long _timeSpan = DateTime.UtcNow.Ticks;
        private readonly Lazy<bool> _keepAlive;
        private CancellationTokenSource? _exitCts;
    }

    public enum ArcPageStatus {
        /// <summary>
        /// [不可用/未加载]
        /// 页面不在视觉树中 (Grid.Children 不包含此页面)。
        /// 此时页面对象可能已被销毁，或者仅存在于缓存字典中但未挂载。
        /// </summary>
        Stopped,

        /// <summary>
        /// Represents a state indicating that an entity is not yet active but is prepared to become active.
        /// </summary>
        PreActive,

        /// <summary>
        /// [正常运行]
        /// 页面在视觉树中，完全可见，且可以响应用户交互。
        /// 对应：Opacity=1, IsHitTestVisible=True, ZIndex=最高
        /// </summary>
        Active,

        /// <summary>
        /// [被隐藏/后台运行]
        /// 页面依然在视觉树中，UI 线程仍在渲染它（动画、WebView 均在运行），
        /// 但用户看不见，且无法点击。
        /// 对应：Opacity=0, IsHitTestVisible=False, ZIndex=较低
        /// </summary>
        BackgroundRunning
    }
}
