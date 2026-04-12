using System.Threading.Tasks;
using K4os.Compression.LZ4;

namespace VirtualPaper.Common.Utils.Archive {
    public static class LZ4Compressor {
        // 支持 Span 的压缩方法
        public static byte[] Compress(ReadOnlySpan<byte> input, LZ4Level level = LZ4Level.L00_FAST) {
            // 计算最大输出大小
            int maxOutputSize = LZ4Codec.MaximumOutputSize(input.Length);
            byte[] output = new byte[maxOutputSize];

            // 执行压缩
            int compressedSize = LZ4Codec.Encode(
                input,
                output.AsSpan(),
                level);

            // 返回实际压缩后的数据
            return compressedSize > 0 ? output.AsSpan(0, compressedSize).ToArray() :
                throw new InvalidOperationException("LZ4 compression failed");
        }

        public static async Task<byte[]> CompressAsync(ReadOnlyMemory<byte> input, LZ4Level level = LZ4Level.L00_FAST) {
            return await Task.Run(() => {
                // 计算最大输出大小
                int maxOutputSize = LZ4Codec.MaximumOutputSize(input.Length);
                byte[] output = new byte[maxOutputSize];

                // 执行压缩
                int compressedSize = LZ4Codec.Encode(
                    input.Span,
                    output.AsSpan(),
                    level);

                // 返回实际压缩后的数据
                return compressedSize > 0 ? output.AsSpan(0, compressedSize).ToArray() :
                    throw new InvalidOperationException("LZ4 compression failed");
            });
        }

        // 支持 Span 的解压方法
        public static byte[] Decompress(ReadOnlySpan<byte> compressed, int originalLength) {
            if (compressed.IsEmpty) return [];

            byte[] output = new byte[originalLength];
            int decompressedSize = LZ4Codec.Decode(
                compressed,
                output.AsSpan());

            return decompressedSize == originalLength ? output :
               throw new InvalidOperationException("LZ4 decompression size mismatch");
        }

        public static async Task<byte[]> DecompressAsync(ReadOnlyMemory<byte> compressed, int originalLength) {
            if (compressed.IsEmpty) return [];

            return await Task.Run(() => {
                byte[] output = new byte[originalLength];
                int decompressedSize = LZ4Codec.Decode(
                    compressed.Span,
                    output.AsSpan());

                return decompressedSize == originalLength ? output :
                   throw new InvalidOperationException("LZ4 decompression size mismatch");
            });
        }

        // 保持原有接口的兼容性
        public static byte[] Compress(byte[] input) =>
            Compress(input.AsSpan());

        public static byte[] Decompress(byte[] compressed, int originalLength) =>
            Decompress(compressed.AsSpan(), originalLength);
    }
}
