using System;
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

        public bool IsDeleted { get; set; }
        public int ZIndex { get; set; }

        ImageSource? _layerThum;
        public ImageSource? LayerThum {
            get { return _layerThum; }
            set { _layerThum = value; OnPropertyChanged(); }
        }

        private readonly Guid _tag = Guid.NewGuid();

        private CanvasImageSource? _thumbSource;

        private void OnContentChanged(object? sender, EventArgs e) {
            UpdateThumbnail();
        }

        private void UpdateThumbnail() {
            try {
                if (RenderData?.RenderTarget == null) return;

                var offscreenRT = new CanvasRenderTarget(RenderData.RenderTarget.Device, 60, 38, 96);

                using (var ds = offscreenRT.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);
                    ds.DrawImage(RenderData.RenderTarget, offscreenRT.Bounds, RenderData.RenderTarget.Bounds, 1f, CanvasImageInterpolation.HighQualityCubic);
                }

                CrossThreadInvoker.InvokeOnUIThread(() => {
                    try {
                        if (_thumbSource == null) {
                            _thumbSource = new CanvasImageSource(RenderData.RenderTarget.Device, 60, 38, 96);
                            LayerThum = _thumbSource;
                        }

                        using (var ds = _thumbSource.CreateDrawingSession(Colors.Transparent)) {
                            ds.Clear(Colors.Transparent);
                            ds.DrawImage(offscreenRT);
                        }
                    }
                    finally {
                        offscreenRT.Dispose();
                    }
                });

            }
            catch (Exception ex) {
                ArcLog.GetLogger<LayerInfo>().Error($"Error updating thumbnail: {ex}");
            }
        }
    }
}
