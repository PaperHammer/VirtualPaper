using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Shader;
using Windows.Graphics.DirectX;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Creation.StaticImg.Models.ToolItems.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, IRuntime {
        internal static MainPage Instance { get; private set; }
        internal Logger Log { get; init; }
        internal IDraftPanelBridge Bridge { get; }
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

        private MainPage(IDraftPanelBridge bridge) {
            this.InitializeComponent();
            Instance = this;
            Bridge = bridge;
            SharedDevice = CanvasDevice.GetSharedDevice();
            SharedFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
            SharedAlphaMode = CanvasAlphaMode.Premultiplied;
            UnReUtil = new StaticImgUndoRedoUtil();
            Log = LogManager.GetCurrentClassLogger();
        }

        /// <summary>
        /// 打开文件
        /// </summary>
        /// <param name="filePath">类型为 FDeign 或静态图像的文件路径</param>
        public MainPage(IDraftPanelBridge bridge, FileType rtFileType, string filePath) : this(bridge) {
            ProjectUtil = ProjectFile.Create(filePath);
            RTFileType = rtFileType;            
        }

        /// <summary>
        /// 新建项目
        /// </summary>
        /// <param name="fileName">项目名</param>
        public MainPage(IDraftPanelBridge bridge, string fileName) : this(bridge) {
            ProjectUtil = ProjectFile.Create(fileName);
            RTFileType = FileType.FDesign;
        }

        private async void Page_Loading(FrameworkElement sender, object args) {
            await ShaderLoader.LoadAllShadersAsync();
        }

        // TODO: 考虑此处 restore
        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            this.IsEnabled = false;
            Bridge.GetNotify().Loading(false, false);

            await InkCanvas.IsInited.Task;            

            StartFrameTimeMonitor();
            Bridge.GetNotify().Loaded();
            this.IsEnabled = true;
        }

        // TODO：切换左侧导航栏时会触发 page_unloaded
        /*
         * 方案一：使用 flag 尝试阻止 unloaded（obsolete）
         * 方案二：使用 flag 尝试再 loaded 时从内存中恢复内容
         * 
         * flag：如果触发 exit 则表示无需保留内容，直接 unloaded-dispose；否则表示内容需要保留，下一次需要恢复
         * 
         */
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
                        // 正常退出
                    }
                    catch (Exception ex) {
                        Bridge.Log(LogType.Error, $"FrameTime monitor error: {ex.Message}");
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
                Bridge.Log(LogType.Error, ex);
                Bridge.GetNotify().ShowExp(ex);
            }
        }


        public async Task UndoAsync() {
            try {
                await UnReUtil.UndoAsync();
            }
            catch (Exception ex) {
                Bridge.Log(LogType.Error, ex);
                Bridge.GetNotify().ShowExp(ex);
            }
        }

        public async Task RedoAsync() {
            try {
                await UnReUtil.RedoAsync();
            }
            catch (Exception ex) {
                Bridge.Log(LogType.Error, ex);
                Bridge.GetNotify().ShowExp(ex);
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
