using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Shader;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Windows.Graphics.DirectX;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Creation.StaticImg.Models.ToolItems.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : ArcPage, IRuntime {
        public override ArcPageContext ArcContext { get; set; }
        public override Type ArcType => typeof(MainPage);

        internal static MainPage Instance { get; private set; }
        //internal ArcPageContext ArcContext { get; private set; }
        internal CanvasDevice SharedDevice { get; }
        internal StaticImgUndoRedoUtil UnReUtil { get; }
        internal ProjectFile ProjectUtil { get; }
        internal FileType RTFileType { get; }
        internal DirectXPixelFormat SharedFormat { get; }
        internal CanvasAlphaMode SharedAlphaMode { get; }
        internal bool IsExited { get; private set; }

        public double FrameTimeMs {
            get { lock (_frameTimeLock) return _frameTimeMs; }
            private set { lock (_frameTimeLock) _frameTimeMs = value; }
        }

        private MainPage() {
            this.InitializeComponent();
            ArcContext = new ArcPageContext(this, this.MainHost.LoadingControlHost);
            Instance = this;
            SharedDevice = CanvasDevice.GetSharedDevice();
            SharedFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
            SharedAlphaMode = CanvasAlphaMode.Premultiplied;
            UnReUtil = new StaticImgUndoRedoUtil();
        }

        /// <summary>
        /// 打开文件
        /// </summary>
        /// <param name="filePath">类型为 FDeign 或静态图像的文件路径</param>
        public MainPage(FileType rtFileType, string filePath) : this() {
            ProjectUtil = ProjectFile.Create(filePath);
            RTFileType = rtFileType;
        }

        /// <summary>
        /// 新建项目
        /// </summary>
        /// <param name="fileName">项目名</param>
        public MainPage(string fileName) : this() {
            ProjectUtil = ProjectFile.Create(fileName);
            RTFileType = FileType.FDesign;
        }

        private async void Page_Loading(FrameworkElement sender, object args) {
            await ShaderLoader.LoadAllShadersAsync();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            this.IsEnabled = false;

            var ctx = ArcPageContextManager.GetContext<MainPage>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            await loadingCtx.RunAsync(
                operation: async token => {
                    await InkCanvas.IsInited.Task;
                });
            
            StartFrameTimeMonitor();

            this.IsEnabled = true;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            StopFrameTimeMonitor();
            SharedDevice.Dispose();
            UnReUtil.Dispose();
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
                await InkCanvas.SaveAsync();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }
        }


        public async Task UndoAsync() {
            try {
                await UnReUtil.UndoAsync();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }
        }

        public async Task RedoAsync() {
            try {
                await UnReUtil.RedoAsync();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }
        }
        #endregion

        private volatile bool _frameTimeRunning = false;
        private Task _frameTimeTask;
        private readonly object _frameTimeLock = new();
        private double _frameTimeMs;
        private DateTime _lastFrameTime;
        private readonly CancellationTokenSource _frameTimeCts = new();
    }
}
