using System.IO.Compression;

namespace VirtualPaper.Common.Extensions {
    public static class ByteExtensions {
        public static byte[] CompressPixels(this byte[] data) {
            if (data.Length == 0) return data;

            using var outputStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(outputStream, CompressionLevel.Fastest)) {
                deflateStream.Write(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }

        public static byte[] DecompressPixels(this byte[] compressedData) {
            if (compressedData.Length == 0) return compressedData;

            using var inputStream = new MemoryStream(compressedData);
            using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            deflateStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
}
