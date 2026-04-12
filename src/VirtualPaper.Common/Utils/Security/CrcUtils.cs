namespace VirtualPaper.Common.Utils.Security {
    public static class CrcUtils {
        private static readonly uint[] CrcTable = new uint[256];

        static CrcUtils() {
            const uint polynomial = 0xEDB88320;
            for (uint i = 0; i < 256; i++) {
                uint crc = i;
                for (int j = 8; j > 0; j--) {
                    if ((crc & 1) == 1)
                        crc = (crc >> 1) ^ polynomial;
                    else
                        crc >>= 1;
                }
                CrcTable[i] = crc;
            }
        }

        public static uint ComputeCrc32(byte[] data, int offset = 0, int length = -1) {
            if (length == -1) length = data.Length - offset;
            uint crc = 0xFFFFFFFF;
            for (int i = offset; i < offset + length; i++) {
                byte index = (byte)((crc & 0xFF) ^ data[i]);
                crc = (crc >> 8) ^ CrcTable[index];
            }
            return ~crc;
        }

        public static uint ComputeCrc32(ReadOnlySpan<byte> data) {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data) {
                byte index = (byte)((crc & 0xFF) ^ b);
                crc = (crc >> 8) ^ CrcTable[index];
            }
            return ~crc;
        }
    }
}
