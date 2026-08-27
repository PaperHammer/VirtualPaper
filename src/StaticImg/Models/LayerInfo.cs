using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace Workloads.Creation.StaticImg.Models {
    public partial class LayerInfo : ObservableObject {
        private InkRenderData _renderData;
        public InkRenderData RenderData {
            get => _renderData;
            set {
                CancelPendingThumbnailUpdate();
                if (_renderData != null) _renderData.OnceRenderCompleted -= OnContentChanged;
                _renderData = value;
                if (_renderData != null) _renderData.OnceRenderCompleted += OnContentChanged;
                OnPropertyChanged();
            }
        }

        public Guid Tag => _tag;

        private string _name = string.Empty;
        public string Name {
            get => _name;
            set { if (_name == value) return; _name = value; OnPropertyChanged(); }
        }

        private bool _isVisible = true;
        public bool IsVisible {
            get => _isVisible;
            set {
                if (_isVisible == value) return;
                _isVisible = value;
                if (value) GlobalMessageUtil.CloseAndRemoveMsg(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), nameof(Constants.I18n.Draft_SI_LayerLocked));
                OnPropertyChanged();
            }
        }

        public bool IsDeleted {
            get => _isDeleted;
            set {
                if (_isDeleted == value) return;
                _isDeleted = value;
                if (value) CancelPendingThumbnailUpdate();
            }
        }
        public int ZIndex { get; set; }

        ImageSource? _layerThum;
        public ImageSource? LayerThum {
            get { return _layerThum; }
            set { _layerThum = value; OnPropertyChanged(); }
        }

        private readonly Guid _tag = Guid.NewGuid();

        private CanvasImageSource? _thumbSource;

        private void OnContentChanged(object? sender, EventArgs e) {
            if (sender is not InkRenderData renderData) return;

            CancellationTokenSource requestCts;
            CancellationTokenSource? previousCts;
            long requestVersion;
            lock (_thumbnailUpdateLock) {
                previousCts = _thumbnailDebounceCts;
                requestCts = new CancellationTokenSource();
                _thumbnailDebounceCts = requestCts;
                requestVersion = Interlocked.Increment(ref _thumbnailRequestVersion);
            }
            CancelSafely(previousCts);

            _ = UpdateThumbnailAfterDelayAsync(renderData, requestVersion, requestCts);
        }

        private async Task UpdateThumbnailAfterDelayAsync(
            InkRenderData renderData,
            long requestVersion,
            CancellationTokenSource requestCts) {
            try {
                await Task.Delay(ThumbnailDebounceDelay, requestCts.Token).ConfigureAwait(false);
                requestCts.Token.ThrowIfCancellationRequested();

                if (!IsThumbnailRequestCurrent(renderData, requestVersion)) return;
                UpdateThumbnail(renderData, requestVersion, requestCts.Token);
            }
            catch (OperationCanceledException) when (requestCts.IsCancellationRequested) {
                // 后续内容变更已经替代本次缩略图请求。
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LayerInfo>().Error($"Error scheduling thumbnail update: {ex}");
            }
            finally {
                lock (_thumbnailUpdateLock) {
                    if (ReferenceEquals(_thumbnailDebounceCts, requestCts)) {
                        _thumbnailDebounceCts = null;
                    }
                }
                requestCts.Dispose();
            }
        }

        private void UpdateThumbnail(
            InkRenderData renderData,
            long requestVersion,
            CancellationToken token) {
            CanvasRenderTarget? offscreenRT = null;
            bool isDispatchedToUi = false;
            try {
                if (!IsThumbnailRequestCurrent(renderData, requestVersion)) return;

                CanvasRenderTarget source = renderData.RenderTarget;

                offscreenRT = new CanvasRenderTarget(
                    source.Device,
                    Consts.LayerThumWidth,
                    Consts.LayerThumHeight,
                    96);

                using (var ds = offscreenRT.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);
                    ds.DrawImage(
                        source,
                        offscreenRT.Bounds,
                        source.Bounds,
                        1f,
                        CanvasImageInterpolation.HighQualityCubic);
                }

                token.ThrowIfCancellationRequested();
                if (!IsThumbnailRequestCurrent(renderData, requestVersion)) return;

                CanvasRenderTarget completedThumbnail = offscreenRT;
                CrossThreadInvoker.InvokeOnUIThread(() => {
                    try {
                        if (!IsThumbnailRequestCurrent(renderData, requestVersion)) return;

                        if (_thumbSource == null) {
                            _thumbSource = new CanvasImageSource(
                                completedThumbnail.Device,
                                Consts.LayerThumWidth,
                                Consts.LayerThumHeight,
                                96);
                            LayerThum = _thumbSource;
                        }

                        using (var ds = _thumbSource.CreateDrawingSession(Colors.Transparent)) {
                            ds.Clear(Colors.Transparent);
                            ds.DrawImage(completedThumbnail);
                        }
                    }
                    finally {
                        completedThumbnail.Dispose();
                    }
                });
                isDispatchedToUi = true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                // 防抖期间产生了更新的缩略图请求。
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LayerInfo>().Error($"Error updating thumbnail: {ex}");
            }
            finally {
                if (!isDispatchedToUi) offscreenRT?.Dispose();
            }
        }

        private bool IsThumbnailRequestCurrent(InkRenderData renderData, long requestVersion) {
            return requestVersion == Volatile.Read(ref _thumbnailRequestVersion)
                && ReferenceEquals(_renderData, renderData)
                && !Volatile.Read(ref _isDeleted);
        }

        private void CancelPendingThumbnailUpdate() {
            CancellationTokenSource? pendingCts;
            lock (_thumbnailUpdateLock) {
                Interlocked.Increment(ref _thumbnailRequestVersion);
                pendingCts = _thumbnailDebounceCts;
                _thumbnailDebounceCts = null;
            }
            CancelSafely(pendingCts);
        }

        private static void CancelSafely(CancellationTokenSource? cts) {
            try {
                cts?.Cancel();
            }
            catch (ObjectDisposedException) {
                // 请求已在完成路径释放，无需再次取消。
            }
        }

        private static readonly TimeSpan ThumbnailDebounceDelay = TimeSpan.FromMilliseconds(150);
        private readonly object _thumbnailUpdateLock = new();
        private CancellationTokenSource? _thumbnailDebounceCts;
        private long _thumbnailRequestVersion;
        private bool _isDeleted;
    }
}
