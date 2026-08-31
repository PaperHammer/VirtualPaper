using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Core.UndoRedoCommand {
    /// <summary>
    /// Represents an undoable command that rebuilds a specific layer in the ink canvas by updating its size and
    /// pixel data.
    /// </summary>
    /// <remarks>This command enables asynchronous execution and supports undo functionality, allowing
    /// changes to a layer's dimensions and pixel content to be reverted. It is typically used to apply or revert
    /// modifications to a layer's visual state within the canvas, ensuring that rendering updates are properly
    /// requested after each operation.</remarks>
    public record LayerRebuildCommand : IUndoableCommand, IMemoryAwareUndoableCommand, IDiskSpillableUndoCommand, IDisposable {
        public string Description { get; } = "Layer Rebuild";
        public long EstimatedMemoryBytes =>
            EstimateMemory(_originalPixels) +
            EstimateMemory(_newPixels) +
            256;
        long IDiskSpillableUndoCommand.DiskStorageBytes =>
            EstimateDiskStorage(_originalPixels) + EstimateDiskStorage(_newPixels);

        public LayerRebuildCommand(
            InkCanvasData canvasData,
            ArcSize originalSize,
            ArcSize newSize,
            Dictionary<Guid, byte[]> compressedOriginalPixels,
            Dictionary<Guid, byte[]> compressedNewPixels,
            Action requestRenderAction) {

            _canvasData = canvasData;
            _originalSize = originalSize;
            _newSize = newSize;
            _originalPixels = CreatePayloads(compressedOriginalPixels);
            _newPixels = CreatePayloads(compressedNewPixels);
            _requestRenderAction = requestRenderAction;
        }

        public async Task ExecuteAsync() {
            var tasks = _canvasData.Layers
                .Where(ink => ink.RenderData != null)
                .Select(ink => Task.Run(async () => {
                    if (_newPixels.TryGetValue(ink.Tag, out UndoDiskPayload? payload)) {
                        byte[] pixels = await ReadPixelsAsync(payload);
                        ink.RenderData.ResizeAndSetPixels(_newSize, pixels);
                        ink.RenderData.HandleOnceRenderCompleted();
                    }
                }));
            await Task.WhenAll(tasks);

            _canvasData.CanvasSize = _newSize;
            _requestRenderAction?.Invoke();
        }

        public async Task UndoAsync() {
            var tasks = _canvasData.Layers
                .Where(ink => ink.RenderData != null)
                .Select(ink => Task.Run(async () => {
                    if (_originalPixels.TryGetValue(ink.Tag, out UndoDiskPayload? payload)) {
                        byte[] pixels = await ReadPixelsAsync(payload);
                        ink.RenderData.ResizeAndSetPixels(_originalSize, pixels);
                        ink.RenderData.HandleOnceRenderCompleted();
                    }
                }));
            await Task.WhenAll(tasks);

            _canvasData.CanvasSize = _originalSize;
            _requestRenderAction?.Invoke();
        }

        private readonly InkCanvasData _canvasData;
        private readonly ArcSize _originalSize;
        private readonly ArcSize _newSize;
        private readonly Dictionary<Guid, UndoDiskPayload> _originalPixels;
        private readonly Dictionary<Guid, UndoDiskPayload> _newPixels;
        private readonly Action _requestRenderAction;

        async Task<bool> IDiskSpillableUndoCommand.TrySpillToDiskAsync(UndoDiskStore store, CancellationToken cancellationToken) {
            bool spilled = await SpillPayloadsAsync(_originalPixels, store, cancellationToken);
            return await SpillPayloadsAsync(_newPixels, store, cancellationToken) || spilled;
        }

        private static Dictionary<Guid, UndoDiskPayload> CreatePayloads(IReadOnlyDictionary<Guid, byte[]> snapshots) =>
            snapshots.ToDictionary(
                static pair => pair.Key,
                static pair => new UndoDiskPayload(pair.Value));

        private static async Task<byte[]> ReadPixelsAsync(UndoDiskPayload payload) {
            byte[] compressedPixels = await payload.ReadAsync();
            return compressedPixels.DecompressPixels();
        }

        private static async Task<bool> SpillPayloadsAsync(IReadOnlyDictionary<Guid, UndoDiskPayload> payloads, UndoDiskStore store, CancellationToken cancellationToken) {
            bool spilled = false;
            foreach (UndoDiskPayload payload in payloads.Values)
                spilled = await payload.TrySpillToDiskAsync(store, cancellationToken) || spilled;
            return spilled;
        }

        private static void DisposePayloads(IReadOnlyDictionary<Guid, UndoDiskPayload> payloads) {
            foreach (UndoDiskPayload payload in payloads.Values) payload.Dispose();
        }

        private static long EstimateMemory(IReadOnlyDictionary<Guid, UndoDiskPayload> payloads) =>
            payloads.Values.Sum(static payload => payload.ResidentMemoryBytes + 64L);

        private static long EstimateDiskStorage(IReadOnlyDictionary<Guid, UndoDiskPayload> payloads) =>
            payloads.Values.Sum(static payload => payload.DiskStorageBytes);

        #region dispose
        private bool _isDisposed;

        protected virtual void Dispose(bool disposing) {
            if (_isDisposed) return;
            if (disposing) {
                DisposePayloads(_originalPixels);
                DisposePayloads(_newPixels);
            }
            _isDisposed = true;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }
}
