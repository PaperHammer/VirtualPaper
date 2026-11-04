using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Core.UndoRedoCommand {
    public class LayerResizeScaleCommand : IUndoableCommand, IMemoryAwareUndoableCommand, IDiskSpillableUndoCommand {
        public string Description { get; } = "Layer Reisze or Scale";
        public long EstimatedMemoryBytes =>
            EstimateMemory(_originalPixels) +
            EstimateMemory(_newPixels) +
            256;
        long IDiskSpillableUndoCommand.DiskStorageBytes =>
            EstimateDiskStorage(_originalPixels) + EstimateDiskStorage(_newPixels);

        private readonly InkCanvasData _canvasData;
        private readonly ArcSize _originalSize;
        private readonly ArcSize _newSize;
        private Dictionary<Guid, UndoDiskPayload>? _originalPixels;
        private Dictionary<Guid, UndoDiskPayload>? _newPixels;
        private readonly Action<ArcSize> _requestRenderAction;
        private bool _isFirstExecution = true;

        public LayerResizeScaleCommand(
            InkCanvasData canvasData,
            ArcSize originalSize,
            ArcSize newSize,
            Action<ArcSize> requestRenderAction) {
            _canvasData = canvasData;
            _originalSize = originalSize;
            _newSize = newSize;
            _requestRenderAction = requestRenderAction;
        }

        public async Task ExecuteAsync() {
            if (_isFirstExecution) {
                _isFirstExecution = false;

                var originalPixelsDict = new ConcurrentDictionary<Guid, byte[]>();
                var tasks = _canvasData.Layers
                    .Where(ink => ink.RenderData != null)
                    .Select(async ink => {
                        byte[] compressedOld = ink.RenderData.RenderTarget.GetPixelBytes().CompressPixels();
                        originalPixelsDict.TryAdd(ink.Tag, compressedOld);
                        await ink.RenderData.ResizeRenderTargetAsync(_newSize);
                        ink.RenderData.HandleOnceRenderCompleted();
                    });
                await Task.WhenAll(tasks);
                _originalPixels = CreatePayloads(originalPixelsDict);

                var newPixelsDict = new ConcurrentDictionary<Guid, byte[]>();
                Parallel.ForEach(_canvasData.Layers, item => {
                    if (item.RenderData?.RenderTarget != null) {
                        byte[] compressedNew = item.RenderData.RenderTarget.GetPixelBytes().CompressPixels();
                        newPixelsDict.TryAdd(item.Tag, compressedNew);
                    }
                });
                _newPixels = CreatePayloads(newPixelsDict);

                _canvasData.CanvasSize = _newSize;
                _requestRenderAction?.Invoke(_newSize);
            }
            else {
                if (_newPixels == null) return;

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
                _requestRenderAction?.Invoke(_newSize);
            }
        }

        public async Task UndoAsync() {
            if (_originalPixels == null) return;

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
            _requestRenderAction?.Invoke(_originalSize);
        }

        async Task<bool> IDiskSpillableUndoCommand.TrySpillToDiskAsync(
            UndoDiskStore store,
            CancellationToken cancellationToken) {
            bool spilled = await SpillPayloadsAsync(_originalPixels, store, cancellationToken);
            return await SpillPayloadsAsync(_newPixels, store, cancellationToken) || spilled;
        }

        public void Dispose() {
            DisposePayloads(_originalPixels);
            DisposePayloads(_newPixels);
            _originalPixels = null;
            _newPixels = null;
        }

        private static Dictionary<Guid, UndoDiskPayload> CreatePayloads(
            IEnumerable<KeyValuePair<Guid, byte[]>> snapshots) =>
            snapshots.ToDictionary(
                static pair => pair.Key,
                static pair => new UndoDiskPayload(pair.Value));

        private static async Task<byte[]> ReadPixelsAsync(UndoDiskPayload payload) {
            byte[] compressedPixels = await payload.ReadAsync();
            return compressedPixels.DecompressPixels();
        }

        private static async Task<bool> SpillPayloadsAsync(
            IReadOnlyDictionary<Guid, UndoDiskPayload>? payloads,
            UndoDiskStore store,
            CancellationToken cancellationToken) {
            if (payloads == null) return false;
            bool spilled = false;
            foreach (UndoDiskPayload payload in payloads.Values)
                spilled = await payload.TrySpillToDiskAsync(store, cancellationToken) || spilled;
            return spilled;
        }

        private static void DisposePayloads(IReadOnlyDictionary<Guid, UndoDiskPayload>? payloads) {
            if (payloads == null) return;
            foreach (UndoDiskPayload payload in payloads.Values) payload.Dispose();
        }

        private static long EstimateMemory(IReadOnlyDictionary<Guid, UndoDiskPayload>? payloads) =>
            payloads?.Values.Sum(static payload => payload.ResidentMemoryBytes + 64L) ?? 0;

        private static long EstimateDiskStorage(IReadOnlyDictionary<Guid, UndoDiskPayload>? payloads) =>
            payloads?.Values.Sum(static payload => payload.DiskStorageBytes) ?? 0;
    }
}
