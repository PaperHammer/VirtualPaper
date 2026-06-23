using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using VirtualPaper.Shader;
using VirtualPaper.Shader.Core;
using VirtualPaper.Shader.Models;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Core.UndoRedoCommand {
    /// <summary>
    /// 效果应用的撤销/重做命令。存储效果参数和压缩后的原始图层数据
    /// </summary>
    public record EffectCommand : IUndoableCommand {
        public string Description { get; }

        public EffectCommand(
            Guid layerId,
            InkCanvasData canvasData,
            ShaderType shaderType,
            EffectParams effectParams,
            byte[] compressedOriginalPixels,
            string description,
            Action requestRenderAction) {
            _layerId = layerId;
            _canvasData = canvasData;
            _shaderType = shaderType;
            _effectParams = effectParams;
            _compressedOriginalPixels = compressedOriginalPixels;
            Description = description;
            _requestRenderAction = requestRenderAction;
        }

        public Task ExecuteAsync() {
            RestoreOriginal();
            ApplyEffect(_effectParams);
            return Task.CompletedTask;
        }

        public Task UndoAsync() {
            RestoreOriginal();
            return Task.CompletedTask;
        }

        private void ApplyEffect(EffectParams effectParams) {
            var renderData = _canvasData.Layers.FirstOrDefault(l => l.Tag == _layerId)?.RenderData;
            if (renderData?.RenderTarget == null) return;

            var rt = renderData.RenderTarget;
            using var temp = new CanvasRenderTarget(rt, rt.SizeInPixels.Width, rt.SizeInPixels.Height, rt.Dpi, rt.Format, rt.AlphaMode);
            using (var ds = temp.CreateDrawingSession())
                ds.DrawImage(rt);

            var result = EffectApplier.Apply(_shaderType, effectParams, temp);
            using (var ds = rt.CreateDrawingSession()) {
                ds.Clear(Microsoft.UI.Colors.Transparent);
                ds.DrawImage(result);
            }

            _requestRenderAction?.Invoke();
            renderData.HandleOnceRenderCompleted();
        }

        private void RestoreOriginal() {
            var renderData = _canvasData.Layers.FirstOrDefault(l => l.Tag == _layerId)?.RenderData;
            if (renderData?.RenderTarget == null) return;

            var originalPixels = _compressedOriginalPixels.DecompressPixels();
            renderData.RenderTarget.SetPixelBytes(originalPixels);

            _requestRenderAction?.Invoke();
            renderData.HandleOnceRenderCompleted();
        }

        private readonly Guid _layerId;
        private readonly InkCanvasData _canvasData;
        private readonly ShaderType _shaderType;
        private readonly EffectParams _effectParams;
        private readonly byte[] _compressedOriginalPixels;
        private readonly Action _requestRenderAction;
    }
}
