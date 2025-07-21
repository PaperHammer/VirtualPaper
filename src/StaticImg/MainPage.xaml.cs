using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.Bridge;
using Windows.Graphics.DirectX;
using Workloads.Creation.StaticImg.Models.ToolItems.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, IRuntime {
        internal static MainPage Instance { get; private set; }
        internal IDraftPanelBridge Bridge { get; }
        internal CanvasDevice SharedDevice { get; }
        internal SI_UndoRedoUtil UnReUtil { get; }
        internal string EntryFilePath { get; }
        internal FileType RTFileType { get; }
        internal DirectXPixelFormat SharedFormat { get; }
        internal CanvasAlphaMode SharedAlphaMode { get; }

        public double FrameTimeMs {
            get { lock (_frameTimeLock) return _frameTimeMs; }
            private set { lock (_frameTimeLock) _frameTimeMs = value; }
        }

        /// <summary>
        /// 静态图像工作页面
        /// </summary>
        /// <param name="entryFilePath">接收类型为 FImage or FE_STATIC_IMG_PROJ 的文件路径</param>
        public MainPage(IDraftPanelBridge bridge, string entryFilePath, FileType rtFileType) {
            Instance = this;
            Bridge = bridge;
            EntryFilePath = entryFilePath;
            RTFileType = rtFileType;
            SharedDevice = CanvasDevice.GetSharedDevice();
            SharedFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
            SharedAlphaMode = CanvasAlphaMode.Premultiplied;
            UnReUtil = new SI_UndoRedoUtil();

            this.InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            this.IsEnabled = false;
            Bridge.GetNotify().Loading(false, false);

            await InkCanvas.IsInited.Task;

            StartFrameTimeMonitor();
            Bridge.GetNotify().Loaded();
            this.IsEnabled = true;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            StopFrameTimeMonitor();
            SharedDevice.Dispose();
            UnReUtil.Dispose();
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
