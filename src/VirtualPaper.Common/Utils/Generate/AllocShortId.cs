namespace VirtualPaper.Common.Utils.Generate {
    public static unsafe class AllocShortId {
        private static long _lastTimestamp;
        private static int _counter;

        public static string Next() {
            const int bufferSize = 15;
            char* buffer = stackalloc char[bufferSize];

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int counter = GetNextCounter(timestamp);

            // 直接写入栈内存
            HexFormatter.Write(buffer, timestamp, 11);
            HexFormatter.Write(buffer + 11, counter, 4);

            return new string(buffer, 0, bufferSize);
        }

        private static int GetNextCounter(long currentTimestamp) {
            while (true) {
                long lastTimestamp = Interlocked.Read(ref _lastTimestamp);

                // 处理时钟回拨
                if (currentTimestamp < lastTimestamp) {
                    Thread.SpinWait(1);
                    currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    continue;
                }

                // 时间戳前进时重置计数器
                if (currentTimestamp > lastTimestamp) {
                    if (Interlocked.CompareExchange(ref _lastTimestamp, currentTimestamp, lastTimestamp) == lastTimestamp)
                        return 0; // 新时间段的第一个ID
                    continue;
                }

                // 相同时间戳内安全递增
                return (Interlocked.Increment(ref _counter) - 1) & 0xFFFF;
            }
        }

        private static class HexFormatter {
            public static unsafe void Write(char* ptr, long value, int length) {
                for (int i = length - 1; i >= 0; i--) {
                    ptr[i] = "0123456789abcdef"[(int)(value & 0xF)];
                    value >>= 4;
                }
            }
        }
    }
}
