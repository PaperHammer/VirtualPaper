using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Converters {
    /// <summary>
    /// 通用值 → bool 转换器。
    /// 支持以下特性：
    /// 1. bool 值：true/false；
    /// 2. Enum 或字符串匹配：当值等于任一参数时为 true（支持多参数逗号分隔）；
    /// 3. “!” 前缀参数：逻辑取反；
    /// 4. 参数 "HasValue"：值非空时为 true；
    /// 5. Debug 标签：当参数中包含 "Debug" 且 DebugEnabled 为 true 时强制返回 true；
    /// 6. 默认行为：非空即 true；
    /// 7. ConvertBack：bool → Visibility，含 “!” 和 Debug 逻辑。
    /// </summary>
    public partial class BoolByValueConverter : IValueConverter {
        /// <summary>
        /// 全局 Debug 开关，开启后当参数包含 "Debug" 时该条绑定将返回 true。
        /// </summary>
        public static bool DebugEnabled { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, string language) {
            string paramRaw = parameter as string ?? string.Empty;

            // “!” 前缀：整体取反
            bool negateParam = false;
            if (!string.IsNullOrEmpty(paramRaw) && paramRaw.StartsWith('!')) {
                negateParam = true;
                paramRaw = paramRaw[1..];
            }

            // 拆分多参数（逗号分隔）
            var paramList = paramRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            // Debug 检查：任一参数包含 Debug 即返回 true
            if (DebugEnabled && paramList.Any(p => p.Equals("Debug", StringComparison.OrdinalIgnoreCase))) {
                return true;
            }

            bool result;

            // 1️⃣ bool 值
            if (value is bool boolVal) {
                result = boolVal;
            }
            // 2️⃣ "HasValue" 模式
            else if (paramList.Any(p => p.Equals("HasValue", StringComparison.OrdinalIgnoreCase))) {
                result = value switch {
                    null => false,
                    string s => !string.IsNullOrWhiteSpace(s),
                    _ => true
                };
            }
            // 3️⃣ Enum 或字符串匹配（多参数）
            else if (value is Enum enumValue && paramList.Count > 0) {
                result = paramList.Any(p => {
                    try {
                        var targetValue = Enum.Parse(value.GetType(), p, ignoreCase: true);
                        return enumValue.Equals(targetValue);
                    }
                    catch { return false; }
                });
            }
            else if (value is string str && paramList.Count > 0) {
                result = paramList.Any(p => str.Equals(p, StringComparison.OrdinalIgnoreCase));
            }
            // 4️⃣ 默认：非空即 true
            else {
                result = value is not null;
            }

            // 应用 “!” 取反
            if (negateParam)
                result = !result;

            return result;
        }

        /// <summary>
        /// 反向转换：bool → Visibility
        /// 支持：
        /// - bool true → Visible；
        /// - bool false → Collapsed；
        /// - 含 “!” 前缀时取反；
        /// - Debug 模式下，若参数包含 Debug 则始终返回 Visible。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            string param = parameter as string ?? string.Empty;

            bool negateParam = false;
            if (!string.IsNullOrEmpty(param) && param.StartsWith('!')) {
                negateParam = true;
                param = param[1..];
            }

            // Debug 模式
            if (DebugEnabled && param.Contains("Debug", StringComparison.OrdinalIgnoreCase))
                return Visibility.Visible;

            if (targetType == typeof(Visibility)) {
                if (value is bool b) {
                    if (negateParam)
                        b = !b;
                    return b ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return value;
        }
    }
}
