using System;
using System.Linq;
using System.Threading;
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
    public partial record EffectCommand : IUndoableCommand, IMemoryAwareUndoableCommand, IDiskSpillableUndoCommand, IDisposable {
        public string Description { get; }
        public long EstimatedMemoryBytes => _originalPixels.ResidentMemoryBytes + 256;
        long IDiskSpillableUndoCommand.DiskStorageBytes => _originalPixels.DiskStorageBytes;

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
            _originalPixels = new UndoDiskPayload(compressedOriginalPixels);
            Description = description;
            _requestRenderAction = requestRenderAction;
        }

        public async Task ExecuteAsync() {
            await RestoreOriginalAsync();
            ApplyEffect(_effectParams);
        }

        public Task UndoAsync() => RestoreOriginalAsync();

        Task<bool> IDiskSpillableUndoCommand.TrySpillToDiskAsync(
            UndoDiskStore store,
            CancellationToken cancellationToken) =>
            _originalPixels.TrySpillToDiskAsync(store, cancellationToken);

        private void ApplyEffect(EffectParams effectParams) {
            var renderData = _canvasData.Layers.FirstOrDefault(l => l.Tag == _layerId)?.RenderData;
            if (renderData?.RenderTarget == null) return;

            var rt = renderData.RenderTarget;
            using var temp = new CanvasRenderTarget(rt, rt.SizeInPixels.Width, rt.SizeInPixels.Height, rt.Dpi, rt.Format, rt.AlphaMode);
            using (var ds = temp.CreateDrawingSession())
                ds.DrawImage(rt);

            var result = EffectApplier.Apply(_shaderType, effectParams, temp);
            try {
                using (var ds = rt.CreateDrawingSession()) {
                    ds.Clear(Microsoft.UI.Colors.Transparent);
                    ds.DrawImage(result);
                }
            }
            finally {
                if (!ReferenceEquals(result, temp))
                    (result as IDisposable)?.Dispose();
            }

            _requestRenderAction?.Invoke();
            renderData.HandleOnceRenderCompleted();
        }

        private async Task RestoreOriginalAsync() {
            byte[] compressedPixels = await _originalPixels.ReadAsync();
            byte[] originalPixels = await Task.Run(compressedPixels.DecompressPixels);

            // Disk I/O can yield. Resolve the layer again afterwards so a layer
            // replacement does not leave us writing to a stale WinRT resource.
            var renderData = _canvasData.Layers.FirstOrDefault(l => l.Tag == _layerId)?.RenderData;
            if (renderData?.RenderTarget == null) return;
            renderData.RenderTarget.SetPixelBytes(originalPixels);

            _requestRenderAction?.Invoke();
            renderData.HandleOnceRenderCompleted();
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _originalPixels.Dispose();
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly Guid _layerId;
        private readonly InkCanvasData _canvasData;
        private readonly ShaderType _shaderType;
        private readonly EffectParams _effectParams;
        private readonly UndoDiskPayload _originalPixels;
        private readonly Action _requestRenderAction;
    }
}
