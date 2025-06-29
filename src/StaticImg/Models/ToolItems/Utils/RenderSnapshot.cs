using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.Archive;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Models.ToolItems.Utils {
    public class RenderSnapshot {
        private long _tag;
        public long Tag {
            get => _tag;
            init => _tag = value;
        }
        
        public Rect AffectedRegion { get; private set; } = Rect.Empty;

        public RenderSnapshot(long tag, CanvasRenderTarget source) {
            Tag = tag;
            _fullOrigin = source.Bounds;
            var bytes = GetPixels(source, _fullOrigin);
            _originPixelsLength = bytes.Length;
            _originPixelsCompressed = LZ4Compressor.Compress(bytes.AsSpan());
        }

        public void RenderToTarget(CanvasRenderTarget target, SnapshotMode mode) {
            if (target == null || Tag == -1) return;

            (var pixels, var region, var length) = mode == SnapshotMode.Origin ?
                (_originPixelsCompressed, _fullOrigin, _originPixelsLength) : 
                (_destinationPixelsCompressed, AffectedRegion, _destinationPixelsLength);

            target.SetPixelBytes(LZ4Compressor.Decompress(pixels, length).AsBuffer(),
                (int)region.Left, (int)region.Top,
                (int)region.Width, (int)region.Height);
        }

        public void UpdateSnapshotData(Rect dirtyRegion) {
            if (dirtyRegion.Width <= 0 || dirtyRegion.Height <= 0)
                return;

            AffectedRegion = AffectedRegion.IsEmpty ? dirtyRegion : AffectedRegion.UnionRect(dirtyRegion).IntersectRect(_fullOrigin);
        }

        public void Commit(CanvasRenderTarget renderTarget) {
            _destinationPixelsLength = (int)(AffectedRegion.Width * AffectedRegion.Height) * 4; // RGBA格式
            var bytes = new byte[_destinationPixelsLength];

            renderTarget.GetPixelBytes(
                bytes.AsBuffer(0, _destinationPixelsLength),
                (int)AffectedRegion.Left, (int)AffectedRegion.Top,
                (int)AffectedRegion.Width, (int)AffectedRegion.Height);
            
            _destinationPixelsCompressed = LZ4Compressor.Compress(bytes.AsSpan());
        }
        public static byte[] GetPixels(CanvasRenderTarget target, Rect area) {
            int pixelCount = (int)(area.Width * area.Height);
            int bufferSize = pixelCount * 4;

            byte[] pixels = new byte[bufferSize];
            var buffer = pixels.AsBuffer();
            target.GetPixelBytes(buffer, (int)area.Left, (int)area.Top, (int)area.Width, (int)area.Height);

            return pixels;
        }

        private readonly byte[] _originPixelsCompressed;
        private byte[]? _destinationPixelsCompressed;
        private readonly int _originPixelsLength;
        private int _destinationPixelsLength;
        private readonly Rect _fullOrigin;
    }

    public enum SnapshotMode {
        Origin,
        Destination
    }
}
