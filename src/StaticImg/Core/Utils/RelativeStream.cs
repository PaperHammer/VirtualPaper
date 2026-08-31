using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Workloads.Creation.StaticImg.Core.Utils {
    /// <summary>
    /// 将可寻址流的一段区域暴露为从 0 开始的独立流。
    /// 固定长度模式用于限制读取范围；非固定长度模式允许从指定起点继续写入。
    /// </summary>
    internal sealed class RelativeStream : Stream {
        public RelativeStream(
            Stream baseStream,
            long origin,
            long? fixedLength = null,
            bool leaveOpen = true) {
            ArgumentNullException.ThrowIfNull(baseStream);
            if (!baseStream.CanSeek)
                throw new ArgumentException("基础流必须支持定位。", nameof(baseStream));
            if (origin < 0 || origin > baseStream.Length)
                throw new ArgumentOutOfRangeException(nameof(origin));
            if (fixedLength is < 0 ||
                fixedLength.HasValue && origin + fixedLength.Value > baseStream.Length)
                throw new ArgumentOutOfRangeException(nameof(fixedLength));

            _baseStream = baseStream;
            _origin = origin;
            _fixedLength = fixedLength;
            _leaveOpen = leaveOpen;
        }

        public override bool CanRead => !_disposed && _baseStream.CanRead;
        public override bool CanSeek => !_disposed && _baseStream.CanSeek;
        public override bool CanWrite => !_disposed && _baseStream.CanWrite && !_fixedLength.HasValue;
        public override long Length {
            get {
                ThrowIfDisposed();
                return _fixedLength ?? Math.Max(0, _baseStream.Length - _origin);
            }
        }
        public override long Position {
            get {
                ThrowIfDisposed();
                return _position;
            }
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush() {
            ThrowIfDisposed();
            _baseStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken) {
            ThrowIfDisposed();
            return _baseStream.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) {
            ValidateRead(buffer, offset, count);
            int permitted = GetPermittedReadCount(count);
            if (permitted == 0) return 0;
            PositionBaseStream();
            int read = _baseStream.Read(buffer, offset, permitted);
            _position += read;
            return read;
        }

        public override int Read(Span<byte> buffer) {
            ThrowIfDisposed();
            if (!CanRead) throw new NotSupportedException();
            int permitted = GetPermittedReadCount(buffer.Length);
            if (permitted == 0) return 0;
            PositionBaseStream();
            int read = _baseStream.Read(buffer[..permitted]);
            _position += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            if (!CanRead) throw new NotSupportedException();
            int permitted = GetPermittedReadCount(buffer.Length);
            if (permitted == 0) return 0;
            PositionBaseStream();
            int read = await _baseStream.ReadAsync(buffer[..permitted], cancellationToken)
                .ConfigureAwait(false);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) {
            ThrowIfDisposed();
            long target = origin switch {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(_position + offset),
                SeekOrigin.End => checked(Length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            if (target < 0 || _fixedLength.HasValue && target > _fixedLength.Value)
                throw new IOException("尝试定位到相对流范围之外。");

            _position = target;
            PositionBaseStream();
            return _position;
        }

        public override void SetLength(long value) {
            ThrowIfDisposed();
            if (!CanWrite) throw new NotSupportedException();
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _baseStream.SetLength(checked(_origin + value));
            if (_position > value) _position = value;
        }

        public override void Write(byte[] buffer, int offset, int count) {
            ValidateWrite(buffer, offset, count);
            PositionBaseStream();
            _baseStream.Write(buffer, offset, count);
            _position += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer) {
            ThrowIfDisposed();
            EnsureWritable(buffer.Length);
            PositionBaseStream();
            _baseStream.Write(buffer);
            _position += buffer.Length;
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            EnsureWritable(buffer.Length);
            PositionBaseStream();
            await _baseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            _position += buffer.Length;
        }

        protected override void Dispose(bool disposing) {
            if (_disposed) return;
            _disposed = true;
            if (disposing && !_leaveOpen) _baseStream.Dispose();
            base.Dispose(disposing);
        }

        private int GetPermittedReadCount(int requested) {
            if (requested < 0) throw new ArgumentOutOfRangeException(nameof(requested));
            long remaining = Length - _position;
            return remaining <= 0 ? 0 : (int)Math.Min(requested, remaining);
        }

        private void PositionBaseStream() => _baseStream.Position = checked(_origin + _position);

        private void ValidateRead(byte[] buffer, int offset, int count) {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (buffer.Length - offset < count) throw new ArgumentException("缓冲区范围无效。");
            if (!CanRead) throw new NotSupportedException();
        }

        private void ValidateWrite(byte[] buffer, int offset, int count) {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (buffer.Length - offset < count) throw new ArgumentException("缓冲区范围无效。");
            EnsureWritable(count);
        }

        private void EnsureWritable(int count) {
            if (!CanWrite) throw new NotSupportedException();
            _ = checked(_position + count);
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        private readonly Stream _baseStream;
        private readonly long _origin;
        private readonly long? _fixedLength;
        private readonly bool _leaveOpen;
        private long _position;
        private bool _disposed;
    }
}
