using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Shader;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Core.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : ArcPage, IRuntime {
        public override Type ArcType => typeof(MainPage);
        protected override bool IsMultiInstance => true;
        public InkProjectSession Session { get; private set; }

        public double FrameTimeMs {
            get { lock (_frameTimeLock) return _frameTimeMs; }
            private set { lock (_frameTimeLock) _frameTimeMs = value; }
        }

        /// <summary>
        /// 打开文件
        /// </summary>
        /// <param name="filePath">类型为 FDeign 或静态图像的文件路径</param>
        public MainPage(string file, FileType rtFileType) {            
            Session = new InkProjectSession(file, rtFileType);
            Payload = new FrameworkPayload() {
                [NaviPayloadKey.ArcPageContext] = this.ArcContext,
                [NaviPayloadKey.InkProjectSession] = this.Session
            };
            this.InitializeComponent();
            ArcContext.AttachLoadingComponent(this.MainHost.LoadingControlHost);
        }

        /// <summary>
        /// 新建项目
        /// </summary>
        /// <param name="fileName">项目名</param>
        public MainPage(string fileName) : this(fileName, FileType.FDesign) { }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            this.IsEnabled = false;

            var ctx = ArcPageContextManager.GetContext(ContextKey);
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            await loadingCtx.RunAsync(
                operation: async token => {
                    await ShaderLoader.LoadAllShadersAsync();
                    await inkCanvas.IsInited.Task;
                });
            
            StartFrameTimeMonitor();

            this.IsEnabled = true;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            StopFrameTimeMonitor();
            Session.Dispose();
            ShaderLoader.ClearCache();
        }

        private void StartFrameTimeMonitor() {
            if (_frameTimeRunning) return;

            _frameTimeRunning = true;
            _frameTimeTask = Task.Run(async () => {
                while (_frameTimeRunning && !_frameTimeCts.IsCancellationRequested) {
                    try {
                        var now = DateTime.Now;
                        if (_lastFrameTime != default) {
                            FrameTimeMs = (now - _lastFrameTime).TotalMilliseconds;
                        }
                        _lastFrameTime = now;

                        // 动态调整采样频率（帧时间越长，采样间隔越大）
                        int delayMs = FrameTimeMs < 16.6 ? 1 : (int)Math.Min(FrameTimeMs / 2, 33);
                        await Task.Delay(delayMs, _frameTimeCts.Token);
                    }
                    catch (TaskCanceledException) {
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<MainPage>().Error($"FrameTime monitor error: {ex.Message}");
                    }
                }
            }, _frameTimeCts.Token);
        }

        private void StopFrameTimeMonitor() {
            _frameTimeRunning = false;
            _frameTimeCts.Cancel();

            try {
                _frameTimeTask?.Wait(50); // 等待50ms确保线程退出
            }
            catch { /* 忽略线程结束时的异常 */ }
        }

        #region workSpace events
        public async Task SaveAsync() {
            try {
                await inkCanvas.SaveAsync();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }
        }


        public async Task UndoAsync() {
            try {
                await Session.UnReUtil.UndoAsync();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }
        }

        public async Task RedoAsync() {
            try {
                await Session.UnReUtil.RedoAsync();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }
        }
        #endregion

        private volatile bool _frameTimeRunning = false;
        private Task? _frameTimeTask;
        private readonly object _frameTimeLock = new();
        private double _frameTimeMs;
        private DateTime _lastFrameTime;
        private readonly CancellationTokenSource _frameTimeCts = new();
    }
}
