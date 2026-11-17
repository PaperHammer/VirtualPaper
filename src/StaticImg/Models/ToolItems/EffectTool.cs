using System;
using Microsoft.Graphics.Canvas;
using Windows.UI;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Logging;
using VirtualPaper.Shader;
using VirtualPaper.Shader.Core;
using VirtualPaper.Shader.Models;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Extensions;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    /// <summary>
    /// 效果工具——在活动图层上实时预览效果，Commit 确认 / Restore 还原。不参与指针交互，仅负责渲染。
    /// </summary>
    public sealed partial class EffectTool : RenderBase {
        private ShaderType _shaderType = ShaderType.None;
        private EffectParams _params = EffectParams.Default;
        private CanvasRenderTarget? _originalCache;
        private CanvasRenderTarget? _previewSource;
        private CanvasRenderTarget? _effectTarget;
        private bool _isPreviewing;

        public bool IsPreviewing => _isPreviewing;
        public ShaderType CurrentShaderType => _shaderType;

        /// <summary>开始预览：缓存当前图层。不立即应用效果，等滑块驱动</summary>
        public bool StartPreview(ShaderType type, EffectParams? param = null) {
            if (!IsCanvasReady) return false;
            try {
                ReleasePreviewResources();
                LayerId = ViewModel.Data.SelectedLayer.Tag;
                _shaderType = type;
                _params = param ?? new EffectParams { Value = 0f, Value2 = 0f, Value3 = 0f, Value4 = 0f, Dpi = 96f };
                _effectTarget = RenderTarget;
                _originalCache = _effectTarget.Clone();
                CreatePreviewSource();
                _isPreviewing = true;
                return true;
            }
            catch (Exception ex) {
                HandleEffectError(ex, "Start effect preview failed");
                ReleasePreviewResources();
                return false;
            }
        }

        /// <summary>更新参数并刷新预览</summary>
        public void UpdateParams(EffectParams param) {
            if (!_isPreviewing) return;
            if (ViewModel.Session.IsDrawingUnavailable) { ReleasePreviewResources(); return; }
            _params = param;
            ApplyEffect(fullQuality: false);
        }

        /// <summary>确认效果：效果已写入 RenderTarget，记录撤销命令，通知缩略图更新</summary>
        public bool Commit(EffectParams? finalParams = null) {
            if (!_isPreviewing || _originalCache == null) return false;
            if (ViewModel.Session.IsDrawingUnavailable) { ReleasePreviewResources(); return false; }
            try {
                if (finalParams.HasValue) _params = finalParams.Value;
                // 应用失败时保留取消入口，不记录一条实际未成功的撤销命令。
                if (!ApplyEffect(fullQuality: true)) return false;
                var originalPixels = _originalCache.GetPixelBytes().CompressPixels();
                var command = new EffectCommand(
                    layerId: LayerId,
                    canvasData: ViewModel.Data,
                    shaderType: _shaderType,
                    effectParams: _params,
                    compressedOriginalPixels: originalPixels,
                    description: $"Apply {_shaderType} effect",
                    requestRenderAction: () => HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion)));
                ViewModel.Session.UnReUtil.RecordCommand(command);
                ReleasePreviewResources();
                RequestOnceRender();
                return true;
            }
            catch (Exception ex) {
                HandleEffectError(ex, "Commit effect failed");
                return false;
            }
        }

        /// <summary>取消效果，还原原始图层</summary>
        public void Cancel() {
            if (!_isPreviewing) return;

            try {
                // 特效会话必须始终还原开始预览时的图层。
                // 切换图层后 RenderBase.RenderTarget 已经指向新图层，不能在这里动态读取。
                if (!ViewModel.Session.IsDrawingUnavailable && _originalCache != null && _effectTarget != null) {
                    _effectTarget.CopyPixelsFromBitmap(_originalCache);
                    HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
                }
            }
            catch (Exception ex) {
                HandleEffectError(ex, "Restore effect preview failed");
            }
            finally {
                ReleasePreviewResources();
            }
        }

        private bool ApplyEffect(bool fullQuality) {
            if (_originalCache == null || _effectTarget == null) return false;

            try {
                CanvasRenderTarget target = _effectTarget;
                // 拖动时使用缩小后的只读源，避免向 GPU 队列持续提交整图特效。
                // 提交时则回到原始尺寸，保证最终质量。
                CanvasRenderTarget source = fullQuality || _previewSource == null
                    ? _originalCache
                    : _previewSource;
                var result = EffectApplier.Apply(_shaderType, _params, source);
                try {
                    using (var ds = target.CreateDrawingSession()) {
                        ds.Clear(Color.FromArgb(0, 0, 0, 0));
                        if (ReferenceEquals(source, _originalCache))
                            ds.DrawImage(result);
                        else
                            ds.DrawImage(result, target.Bounds, source.Bounds);
                    }
                }
                finally {
                    // EffectApplier 会为大多数特效创建 Win2D 效果图。预览每帧都必须释放，
                    // 否则连续拖动会积累 GPU/COM 资源，并在后续 GC 时造成周期性卡顿。
                    if (!ReferenceEquals(result, source))
                        (result as System.IDisposable)?.Dispose();
                }

                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
                if (ViewModel.Session.IsDrawingUnavailable) { ReleasePreviewResources(); return false; }
                return true;
            }
            catch (Exception ex) {
                HandleEffectError(ex, "Apply effect failed");
                return false;
            }
        }

        private void HandleEffectError(Exception error, string operation) {
            if (ViewModel.Session.IsDrawingUnavailable || IsDeviceLost(error)) {
                ReleasePreviewResources();
                if (!ViewModel.Session.IsDrawingUnavailable) HandleDeviceLost();
                return;
            }
            ArcLog.GetLogger<EffectTool>().Error($"{operation}: {_shaderType}", error);
        }

        private void CreatePreviewSource() {
            if (_originalCache == null) return;

            double maxDimension = System.Math.Max(
                _originalCache.SizeInPixels.Width,
                _originalCache.SizeInPixels.Height);
            if (maxDimension <= MaxPreviewDimension) return;

            double scale = MaxPreviewDimension / maxDimension;
            float previewWidth = System.Math.Max(1, (float)(_originalCache.Size.Width * scale));
            float previewHeight = System.Math.Max(1, (float)(_originalCache.Size.Height * scale));
            _previewSource = new CanvasRenderTarget(
                _originalCache,
                previewWidth,
                previewHeight,
                _originalCache.Dpi,
                _originalCache.Format,
                _originalCache.AlphaMode);
            using var ds = _previewSource.CreateDrawingSession();
            ds.Clear(Color.FromArgb(0, 0, 0, 0));
            ds.DrawImage(
                _originalCache,
                _previewSource.Bounds,
                _originalCache.Bounds,
                1f,
                CanvasImageInterpolation.Linear);
        }

        public override void Dispose() {
            ReleasePreviewResources();
            base.Dispose();
        }

        private void ReleasePreviewResources() {
            _isPreviewing = false;
            _shaderType = ShaderType.None;
            _originalCache?.Dispose();
            _originalCache = null;
            _previewSource?.Dispose();
            _previewSource = null;
            _effectTarget = null;
        }

        private const double MaxPreviewDimension = 1280;
    }
}
