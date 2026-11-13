using System;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common.Logging;

namespace Workloads.Creation.StaticImg.Core.Utils {
    /// <summary>One device choice for the main UI's canvases and offscreen resources.</summary>
    public sealed class DrawingDeviceManager : IDisposable {
        public static DrawingDeviceManager Current { get; } = new();

        public DrawingDeviceManager() : this(CanvasDevice.GetSharedDevice) { }

        internal DrawingDeviceManager(Func<bool, CanvasDevice> createDevice) {
            _createDevice = createDevice;
        }

        // Called once at UI startup. Changing the setting takes effect on the next UI start.
        public void Initialize(bool hardwareAccelerationEnabled) {
            lock (_gate) {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_initialized) throw new InvalidOperationException("The drawing device is already initialized.");
                _forceSoftware = !hardwareAccelerationEnabled;
                _initialized = true;
            }
        }

        public CanvasDevice GetDevice() {
            lock (_gate) {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _initialized = true;
                if (_device != null) return _device;
                try {
                    _device = _createDevice(_forceSoftware);
                }
                catch (Exception ex) when (!_forceSoftware && IsDeviceCreationFailure(ex)) {
                    _forceSoftware = true;
                    ArcLog.GetLogger<DrawingDeviceManager>().Warn($"Drawing device initialization failed; using software rendering: {ex.Message}");
                    _device = _createDevice(true);
                }
                _device.DeviceLost += OnDeviceLost;
                return _device;
            }
        }

        internal void OnDeviceLost(CanvasDevice sender, object args) {
            lock (_gate) {
                if (!ReferenceEquals(_device, sender)) return;
                sender.DeviceLost -= OnDeviceLost;
                // Existing projects retain their original device. Only new projects use
                // the replacement: GPU-only layer pixels cannot be silently recreated.
                _device = null;
                _forceSoftware = true;
            }
        }

        internal static bool IsDeviceCreationFailure(Exception error) => error.HResult is
            unchecked((int)0x80004005) or // E_FAIL (driver/device creation)
            unchecked((int)0x887A0004) or // DXGI_ERROR_UNSUPPORTED
            unchecked((int)0x887A0005) or // DXGI_ERROR_DEVICE_REMOVED
            unchecked((int)0x887A0006) or // DXGI_ERROR_DEVICE_HUNG
            unchecked((int)0x887A0007) or // DXGI_ERROR_DEVICE_RESET
            unchecked((int)0x887A0020);   // DXGI_ERROR_DRIVER_INTERNAL_ERROR

        public void Dispose() {
            lock (_gate) {
                if (_disposed) return;
                _disposed = true;
                if (_device != null) _device.DeviceLost -= OnDeviceLost;
                // Win2D owns shared devices; disposing one would invalidate other users.
                _device = null;
            }
        }

        private readonly object _gate = new();
        private readonly Func<bool, CanvasDevice> _createDevice;
        private CanvasDevice? _device;
        private bool _forceSoftware;
        private bool _disposed;
        private bool _initialized;
    }
}
