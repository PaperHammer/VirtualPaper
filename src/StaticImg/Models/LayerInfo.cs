using System;
using Microsoft.Graphics.Canvas;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
using Microsoft.Graphics.Canvas.UI.Xaml;
using VirtualPaper.Common.Utils.ThreadContext;

namespace Workloads.Creation.StaticImg.Models {
    public partial class LayerInfo : ObservableObject {
        private InkRenderData _renderData;
        public InkRenderData RenderData {
            get => _renderData;
            set {
                if (_renderData != null) _renderData.ContentChanged -= OnContentChanged;
                _renderData = value;
                if (_renderData != null) _renderData.ContentChanged += OnContentChanged;
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

        public bool IsDeleted { get; set; }
        public int ZIndex { get; set; }

        ImageSource? _layerThum;
        public ImageSource? LayerThum {
            get { return _layerThum; }
            set { _layerThum = value; OnPropertyChanged(); }
        }

        private readonly Guid _tag = Guid.NewGuid();

        private CancellationTokenSource? _thumbCts;
        private CanvasImageSource? _thumbSource;

        private void OnContentChanged(object? sender, EventArgs e) {
            _thumbCts?.Cancel();
            _thumbCts = new CancellationTokenSource();
            var token = _thumbCts.Token;

            Task.Delay(200, token).ContinueWith(_ => {
                if (token.IsCancellationRequested) return;

                CrossThreadInvoker.InvokeOnUIThread(() => {
                    UpdateThumbnailAsync();
                });
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void UpdateThumbnailAsync() {
            if (RenderData?.RenderTarget == null) return;

            if (_thumbSource == null) {
                _thumbSource = new CanvasImageSource(
                    RenderData.RenderTarget.Device,
                    60, 38, 96);

                LayerThum = _thumbSource;
            }

            // 直接向 ImageSource 的 DrawingSession 绘图
            // 这种方式不需要 GetPixelBytes，数据不经过 CPU，效率最高
            using (var ds = _thumbSource.CreateDrawingSession(Microsoft.UI.Colors.Transparent)) {
                // 使用 Linear 线性插值，对这种提示性小图来说速度和效果最平衡
                ds.DrawImage(RenderData.RenderTarget,
                    new Rect(0, 0, 60, 38),
                    RenderData.RenderTarget.Bounds,
                    1.0f,
                    CanvasImageInterpolation.Linear);
            }

            // 注意：这里不需要再写 LayerThum = _thumbSource，
            // CreateDrawingSession 结束后，WinUI 会自动标记该 Surface 为 Dirty 并重绘。
        }

        //private void OnContentChanged(object? sender, EventArgs e) {
        //    // 防抖：如果 500ms 内没有新的更改，再生成缩略图
        //    _thumbCts?.Cancel();
        //    _thumbCts = new CancellationTokenSource();
        //    var token = _thumbCts.Token;

        //    Task.Delay(500, token).ContinueWith(async _ => {
        //        if (token.IsCancellationRequested) return;
        //        await UpdateThumbnailAsync();
        //    }, TaskScheduler.FromCurrentSynchronizationContext());
        //}

        //private async Task UpdateThumbnailAsync() {
        //    if (RenderData?.RenderTarget == null) return;

        //    // 1. 创建一个小尺寸的 RenderTarget (例如 120x80)
        //    // 建议尺寸根据你的 UI 设计定死
        //    float thumbWidth = 60;
        //    float thumbHeight = 38;

        //    using var thumbTarget = new CanvasRenderTarget(
        //        RenderData.RenderTarget.Device,
        //        thumbWidth,
        //        thumbHeight,
        //        96);

        //    // 2. 将大图缩放绘制到小图
        //    using (var ds = thumbTarget.CreateDrawingSession()) {
        //        ds.Clear(Microsoft.UI.Colors.Transparent);
        //        // Win2D 会自动处理缩放插值
        //        ds.DrawImage(RenderData.RenderTarget, new Rect(0, 0, thumbWidth, thumbHeight));
        //    }

        //    // 3. 转换为 ImageSource (WinUI 3 常用 SoftwareBitmapSource)
        //    var softwareBitmap = await Microsoft.Graphics.Canvas.CanvasBitmap.CreateFromBytes(
        //        thumbTarget.Device,
        //        thumbTarget.GetPixelBytes(),
        //        (int)thumbTarget.SizeInPixels.Width,
        //        (int)thumbTarget.SizeInPixels.Height,
        //        thumbTarget.Format)
        //        .ToSoftwareBitmapAsync(); // 需要扩展方法或手动转换

        //    // 更新 UI 绑定属性
        //    LayerThum = await softwareBitmap.ToImageSourceAsync();
        //}

        //private CancellationTokenSource? _thumbCts;
    }
}
