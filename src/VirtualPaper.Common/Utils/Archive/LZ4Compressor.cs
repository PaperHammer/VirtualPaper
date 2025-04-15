using K4os.Compression.LZ4;

namespace VirtualPaper.Common.Utils.Archive
{
    public static class LZ4Compressor {
        //public static byte[] Compress(byte[] input) {
        //    return LZ4Pickler.Pickle(input);
        //}

        //public static byte[] Decompress(byte[] compressed) {
        //    return LZ4Pickler.Unpickle(compressed);
        //}

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
            if (compressedSize < 0)
                throw new InvalidOperationException("LZ4 compression failed");

            return output.AsSpan(0, compressedSize).ToArray();
        }

        // 支持 Span 的解压方法
        public static byte[] Decompress(ReadOnlySpan<byte> compressed, int originalLength) {
            byte[] output = new byte[originalLength];
            int decompressedSize = LZ4Codec.Decode(
                compressed,
                output.AsSpan());

            if (decompressedSize != originalLength)
                throw new InvalidOperationException("LZ4 decompression failed");

            return output;
        }

        // 保持原有接口的兼容性
        public static byte[] Compress(byte[] input) =>
            Compress(input.AsSpan());

        public static byte[] Decompress(byte[] compressed, int originalLength) =>
            Decompress(compressed.AsSpan(), originalLength);
    }
}
