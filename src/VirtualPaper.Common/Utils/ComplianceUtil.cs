using System.IO;
using System.Text.RegularExpressions;

namespace VirtualPaper.Common.Utils {
    public static partial class ComplianceUtil {
        public static bool IsValidFolderPath(string path, int minLen = 3, int maxLen = 260) {
            if (string.IsNullOrEmpty(path)) {
                return false;
            }

            if (path.Length < minLen || path.Length > maxLen) {
                return false;
            }

            // 快速检查路径的基本格式
            if (!path.StartsWith(@"\\") && (path.Length < 3 || path[1] != ':' || path[2] != '\\')) {
                return false;
            }

            // 检查每个字符是否都在允许的字符集中
            foreach (char c in path) {
                if (!ValidChars.Contains(c)) {
                    return false;
                }
            }

            return true;
        }

        public static bool IsValidName(string value, int minLen = 1, int maxLen = 30) {
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            if (value.Length < minLen || value.Length > maxLen) {
                return false;
            }

            foreach (char c in value) {
                if (!ValidChars.Contains(c)) {
                    return false;
                }
            }

            return true;
        }

        public static bool IsValidValueOnlyLength(string value, int minLen = 1, int maxLen = 30) {
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            if (value.Length < minLen || value.Length > maxLen) {
                return false;
            }

            return true;
        }

        // 不允许空格字符
        private static readonly char[] ValidChars =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.!@#$%^&()[]{}+=-_\\/:"
                .Concat(Enumerable.Range(0x4e00, 0x9fa5 - 0x4e00 + 1).Select(c => (char)c)) // 中文
                .ToArray();
    }
}
