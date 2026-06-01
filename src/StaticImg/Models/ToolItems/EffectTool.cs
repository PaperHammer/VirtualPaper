using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using VirtualPaper.Shader;
using VirtualPaper.Shader.Core;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Events;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    /// <summary>
    /// 效果工具——在活动图层上实时预览效果，Commit 确认 / Cancel 还原。不参与指针交互，仅负责渲染。
    /// </summary>
    public sealed partial class EffectTool : RenderBase {
        private ShaderType _shaderType = ShaderType.None;
        private EffectParams _params = EffectParams.Default;
        private CanvasRenderTarget? _originalCache;
        private bool _isPreviewing;

        public bool IsPreviewing => _isPreviewing;
        public ShaderType CurrentShaderType => _shaderType;

        /// <summary>开始预览：缓存当前图层。不立即应用效果，等滑块驱动</summary>
        public void StartPreview(ShaderType type, EffectParams? param = null) {
            if (!IsCanvasReady) return;

            _shaderType = type;
            _params = param ?? new EffectParams { Value = 0f, Value2 = 0f, Value3 = 0f, Value4 = 0f, Dpi = 96f };

            var rt = RenderTarget;
            _originalCache?.Dispose();
            _originalCache = new CanvasRenderTarget(rt, rt.SizeInPixels.Width, rt.SizeInPixels.Height, rt.Dpi, rt.Format, rt.AlphaMode);
            using (var ds = _originalCache.CreateDrawingSession())
                ds.DrawImage(rt);

            _isPreviewing = true;
        }

        /// <summary>更新参数并刷新预览</summary>
        public void UpdateParams(EffectParams param) {
            if (!_isPreviewing) return;
            _params = param;
            ApplyEffect();
        }

        /// <summary>确认效果：效果已写入 RenderTarget，通知缩略图更新</summary>
        public void Commit() {
            if (!_isPreviewing) return;
            _isPreviewing = false;
            _shaderType = ShaderType.None;
            _originalCache?.Dispose();
            _originalCache = null;
            // 通知 LayerInfo 内容已变化，驱动缩略图刷新
            RequestOnceRender();
        }

        /// <summary>取消效果，还原原始图层</summary>
        public void Cancel() {
            if (!_isPreviewing) return;

            if (_originalCache != null) {
                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);
                    ds.DrawImage(_originalCache);
                }
                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }

            _isPreviewing = false;
            _shaderType = ShaderType.None;
            _originalCache?.Dispose();
            _originalCache = null;
        }

        private void ApplyEffect() {
            if (!IsCanvasReady || _originalCache == null) return;

            // 对缓存（只读）应用效果，避免同时读写 RenderTarget
            var result = EffectApplier.Apply(_shaderType, _params, _originalCache);
            using (var ds = RenderTarget.CreateDrawingSession()) {
                ds.Clear(Colors.Transparent);
                ds.DrawImage(result);
            }

            HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
        }
    }
}
