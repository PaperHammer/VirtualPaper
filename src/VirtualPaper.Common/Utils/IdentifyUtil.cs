using System.Security.Cryptography;
using System.Text;

namespace VirtualPaper.Common.Utils {
    public static class IdentifyUtil {
        public static long GenerateIdShort() {
            byte[] buffer = Guid.NewGuid().ToByteArray();
            return BitConverter.ToInt64(buffer, 0);
        }

        public static int ComputeHash(string input) {
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToInt32(hashBytes, 0);
        }
    }
}
