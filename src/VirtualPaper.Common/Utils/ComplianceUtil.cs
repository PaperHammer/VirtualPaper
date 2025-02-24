using System.Text.RegularExpressions;

namespace VirtualPaper.Common.Utils {
    public static partial class ComplianceUtil {
        public static bool IsValidFolderPath(string path) {
            if (string.IsNullOrEmpty(path)) {
                return false;
            }

            // 检查路径长度
            if (path.Length < MinLength_Folder || path.Length > MaxLength_Folder) {
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

        public static bool IsValidName(string value) {
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            // 检查路径长度
            if (value.Length < MinLength_Name || value.Length > MaxLength_Name) {
                return false;
            }

            if (!NameRegex().IsMatch(value)) {
                return false;
            }        

            return true;
        }


    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled)]
    private static partial Regex NameRegex();

    // 定义允许的字符集合
    private static readonly char[] ValidChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .!@#$%^&()[]{}+=-_\\/:"
        .Concat(Enumerable.Range(0x4e00, 0x9fa5 - 0x4e00 + 1).Select(c => (char)c)).ToArray();

    // 定义路径长度限制
    private const int MinLength_Folder = 3; // 最小长度，例如 "C:\"
    private const int MaxLength_Folder = 260; // Windows传统路径的最大长度限制
    private const int MinLength_Name = 1;
    private const int MaxLength_Name = 30;
}
}
