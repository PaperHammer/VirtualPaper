using MessagePack;

namespace VirtualPaper.Common.Utils.Storage {
    public class BufferSaver<T> {
        public BufferSaver() { }
        
        public BufferSaver(int bufferSizeThreshold) {
            _bufferSizeThreshold = bufferSizeThreshold;
        }

        public async Task SaveToBufferAsync(T data, string filePath) {
            try {
                _filePath = filePath;
                await _saveLock.WaitAsync();
                var serializedData = BufferSaver<T>.SerializeData(data);
                _buffer.Write(serializedData, 0, serializedData.Length);
                if (_buffer.Length > _bufferSizeThreshold) {
                    await FlushBufferToFileAsync();
                }
            }
            finally {
                _saveLock.Release();
            }
        }

        public async Task SaveManuallyAsync() {
            try {
                if (_filePath == string.Empty) return;

                await _saveLock.WaitAsync();
                await FlushBufferToFileAsync();
            }
            finally {
                _saveLock.Release();
            }
        }

        private static byte[] SerializeData(T data) {
            return MessagePackSerializer.Serialize(data);
        }

        private async Task FlushBufferToFileAsync() {
            var bufferData = _buffer.ToArray();
            await BufferSaver<T>.WriteToFileAsync(_filePath, bufferData);
            _buffer.SetLength(0);
        }

        private static async Task WriteToFileAsync(string filePath, byte[] data) {
            using var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None);
            await fileStream.WriteAsync(data);
        }

        private string _filePath = string.Empty;
        private readonly MemoryStream _buffer = new();
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private readonly int _bufferSizeThreshold = 5 * 1024 * 1024; // 5MB
    }
}
