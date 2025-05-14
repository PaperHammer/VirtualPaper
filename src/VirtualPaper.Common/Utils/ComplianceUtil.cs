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

        public static bool IsValidEmail(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }
            return EmailRegex().IsMatch(value);
        }

        public static bool IsValidPwd(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }
            return PwdRegex().IsMatch(value);
        }

        public static bool IsValidUserName(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            if (value.Length < UserNameMinLength || value.Length > UserNameMaxLength) {
                return false;
            }

            // 检查字符集（允许字母、数字、下划线、点、连字符）
            if (!UsernameRegex().IsMatch(value)) {
                return false;
            }

            // 禁止以特殊字符开头或结尾
            if (value.StartsWith('.') || value.StartsWith('-') ||
                value.EndsWith('.') || value.EndsWith('-')) {
                return false;
            }

            // 禁止连续的特殊字符
            if (value.Contains("..") || value.Contains("--") || value.Contains("._") || value.Contains("-.")) {
                return false;
            }

            if (IsBlacklisted(value)) {
                return false;
            }

            return true;
        }

        private static bool IsBlacklisted(string value) {
            return _blacklistedNames.Contains(value.ToLower());
        }

        public static bool IsValidSign(string sign) {
            if (sign.Length > 100) {
                return false;
            }

            return SignRegex().IsMatch(sign);
        }

        private static readonly HashSet<string> _blacklistedNames = ["admin", "root", "test", "guest", "user", "system"];

        // 不允许空格字符
        private static readonly HashSet<char> ValidChars =
            [.. "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.!@#$%^&()[]{}+=-_\\/:"
                .Concat(Enumerable.Range(0x4e00, 0x9fa5 - 0x4e00 + 1).Select(c => (char)c))]; // 中文
                
        private const int UserNameMinLength = 3;
        private const int UserNameMaxLength = 20;

        [GeneratedRegex(@"^[\p{L}0-9_\.-]+$", RegexOptions.Compiled)]
        private static partial Regex UsernameRegex();

        /*
            必须包含一个 @ 符号
            @ 符号前后必须有内容
            @ 符号后必须包含至少一个 .，且 . 后必须有内容
            不能包含非法字符（如空格、特殊符号等）

            ^[^@\s]+：表示 @ 前的部分，不能包含空格或 @，且至少有一个字符。
            @：匹配 @ 符号。
            [^@\s]+：表示 @ 后的部分，不能包含空格或 @，且至少有一个字符。
            \.：匹配一个点号 .。
            [^@\s]+$：表示 . 后的部分，不能包含空格或 @，且至少有一个字符。
         */
        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
        private static partial Regex EmailRegex();

        /*
            长度在 8 到 32 个字符之间。
            至少包含一个大写字母。
            至少包含一个小写字母。
            至少包含一个数字。
            至少包含一个特殊字符（如 !@#$%^&*() 等）

            (?=.*[A-Z])：断言字符串中至少包含一个大写字母。
            (?=.*[a-z])：断言字符串中至少包含一个小写字母。
            (?=.*\d)：断言字符串中至少包含一个数字。
            (?=.*[\W_])：断言字符串中至少包含一个特殊字符（\W 匹配非字母数字字符，_ 单独列出以确保包含下划线）。
            .{8,32}：匹配长度在 8 到 32 个字符之间的字符串。
         */
        [GeneratedRegex(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[\W_]).{8,32}$", RegexOptions.Compiled)]
        private static partial Regex PwdRegex();

        /*
            \p{L} - 任何语言的字母
            \p{N} - 任何数字
            \p{P} - 标点符号
            \p{S} - 特殊符号
            \p{Zs} - 空格分隔符
            {0,100} - 长度限制0到100个字符
         */
        [GeneratedRegex(@"^[\p{L}\p{N}\p{P}\p{S}\p{Zs}]{0,100}$", RegexOptions.Compiled)]
        private static partial Regex SignRegex();
    }
}
