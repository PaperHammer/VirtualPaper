namespace VirtualPaper.Common.Utils.Buffers {
    public class KfifoBuffer<T> {
        public int Count => _count;

        public KfifoBuffer(int capacity) {
            capacity = CalculateNextPowerOfTwo(capacity);
            _buffer = new T[capacity];
            _mask = capacity - 1;
        }

        public bool TryEnqueue(T item) {
            if (_count == _buffer.Length) {
                // 缓冲区满时覆盖最旧数据
                _readPos = (_readPos + 1) & _mask;
                Interlocked.Decrement(ref _count);
            }

            _buffer[_writePos] = item;
            _writePos = (_writePos + 1) & _mask;
            Interlocked.Increment(ref _count);
            return true;
        }

        public bool TryDequeue(out T result) {
            if (_count == 0) {
                result = default;
                return false;
            }

            result = _buffer[_readPos];
            _readPos = (_readPos + 1) & _mask;
            Interlocked.Decrement(ref _count);
            return true;
        }

        private static int CalculateNextPowerOfTwo(int value) {
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        private readonly T[] _buffer;
        private int _readPos = 0;
        private int _writePos = 0;
        private readonly int _mask;
        private volatile int _count = 0;
    }
}
