using BuiltIn.Utils;

namespace BuiltIn.Tool {
    public sealed partial class D2DRenderer : IDisposable {
        private IntPtr _rendererHandle;
        private bool _disposed = false;
        private readonly object _syncRoot = new();

        public int Width { get; }
        public int Height { get; }

        public D2DRenderer(int width, int height) {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Width and height must be positive");

            _rendererHandle = NativeRnder.CreateRenderer(width, height);
            if (_rendererHandle == IntPtr.Zero)
                throw new Exception("Failed to create D2D renderer");

            Width = width;
            Height = height;
        }

        public void Clear(float r, float g, float b, float a = 1.0f) {
            lock (_syncRoot) {
                ThrowIfDisposed();
                var color = new NativeRnder.ColorF { r = r, g = g, b = b, a = a };
                NativeRnder.Clear(_rendererHandle, color);
            }
        }

        public void DrawRectangle(float x, float y, float width, float height,
                                float r, float g, float b, float strokeWidth = 1.0f) {
            lock (_syncRoot) {
                ThrowIfDisposed();
                var topLeft = new NativeRnder.PointF { x = x, y = y };
                var size = new NativeRnder.PointF { x = width, y = height };
                var color = new NativeRnder.ColorF { r = r, g = g, b = b, a = 1.0f };

                NativeRnder.DrawRectangle(_rendererHandle, topLeft, size, color, strokeWidth);
            }
        }

        public void FillRectangle(float x, float y, float width, float height,
                               float r, float g, float b, float a = 1.0f) {
            lock (_syncRoot) {
                ThrowIfDisposed();
                var topLeft = new NativeRnder.PointF { x = x, y = y };
                var size = new NativeRnder.PointF { x = width, y = height };
                var color = new NativeRnder.ColorF { r = r, g = g, b = b, a = a };

                NativeRnder.FillRectangle(_rendererHandle, topLeft, size, color);
            }
        }

        public void BeginErasePath(float x, float y) {
            lock (_syncRoot) {
                ThrowIfDisposed();
                var start = new NativeRnder.PointF { x = x, y = y };
                NativeRnder.BeginErasePath(_rendererHandle, start);
            }
        }

        public void AddEraseLine(float x, float y) {
            lock (_syncRoot) {
                ThrowIfDisposed();
                var end = new NativeRnder.PointF { x = x, y = y };
                NativeRnder.AddEraseLine(_rendererHandle, end);
            }
        }

        public void EndErasePath() {
            lock (_syncRoot) {
                ThrowIfDisposed();
                NativeRnder.EndErasePath(_rendererHandle);
            }
        }

        public byte[] GetPixels() {
            lock (_syncRoot) {
                ThrowIfDisposed();
                int bufferSize = Width * Height * 4; // BGRA32 format
                byte[] pixels = new byte[bufferSize];
                NativeRnder.GetPixels(_rendererHandle, pixels, bufferSize);
                return pixels;
            }
        }

        public void Resize(int newWidth, int newHeight) {
            lock (_syncRoot) {
                ThrowIfDisposed();
                if (newWidth <= 0 || newHeight <= 0)
                    throw new ArgumentException("Width and height must be positive");

                NativeRnder.Resize(_rendererHandle, newWidth, newHeight);
            }
        }

        private void ThrowIfDisposed() {
            ObjectDisposedException.ThrowIf(_disposed, nameof(D2DRenderer));
        }

        public void Dispose() {
            lock (_syncRoot) {
                if (!_disposed) {
                    if (_rendererHandle != IntPtr.Zero) {
                        NativeRnder.DestroyRenderer(_rendererHandle);
                        _rendererHandle = IntPtr.Zero;
                    }
                    _disposed = true;
                }
                GC.SuppressFinalize(this);
            }
        }

        ~D2DRenderer() {
            Dispose();
        }
    }
}
