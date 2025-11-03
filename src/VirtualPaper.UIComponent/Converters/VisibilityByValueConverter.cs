using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Converters {
    /// <summary>
    /// 通用值 → Visibility 转换器。
    /// 支持以下特性：
    /// 1. bool 值：true 显示，false 隐藏；
    /// 2. Enum 或字符串匹配：当值等于参数时显示；
    /// 3. “!” 前缀参数：逻辑取反；
    /// 4. 参数 "HasValue"：值非空时显示；
    /// 5. Debug 标签：当参数中包含 "Debug" 且 DebugEnabled 为 true 时强制显示。
    /// 7. 默认行为：非空即 Visible；
    /// 8. 支持 ConvertBack：Visibility → bool。
    /// </summary>
    public partial class VisibilityByValueConverter : IValueConverter {
        /// <summary>
        /// 全局 Debug 开关，开启后当参数包含 "Debug" 时该条绑定将显示。
        /// </summary>
        public static bool DebugEnabled { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, string language) {
            string param = parameter as string ?? string.Empty;

            // “!” 前缀：取反参数逻辑（先处理前缀）
            bool negateParam = false;
            if (!string.IsNullOrEmpty(param) && param.StartsWith('!')) {
                negateParam = true;
                param = param[1..];
            }

            // Debug 参数检测：只有当参数包含 "Debug" 且全局 DebugEnabled 为 true 时才强制显示
            if (DebugEnabled && param.Contains("Debug", StringComparison.OrdinalIgnoreCase)) {
                return Visibility.Visible;
            }

            bool result;

            // --- 1️ 处理 bool 值 ---
            if (value is bool boolVal) {
                result = boolVal;
            }
            // --- 2️ 处理 "HasValue" 模式 ---
            else if (param.Equals("HasValue", StringComparison.OrdinalIgnoreCase)) {
                result = value switch {
                    null => false,
                    string s => !string.IsNullOrWhiteSpace(s),
                    _ => true
                };
            }
            // --- 3️ Enum 匹配 ---
            else if (value is Enum enumValue && !string.IsNullOrEmpty(param)) {
                try {
                    var targetValue = Enum.Parse(value.GetType(), param, ignoreCase: true);
                    result = enumValue.Equals(targetValue);
                }
                catch {
                    result = false;
                }
            }
            // --- 4️ 字符串匹配 ---
            else if (value is string str && !string.IsNullOrEmpty(param)) {
                result = str.Equals(param, StringComparison.OrdinalIgnoreCase);
            }
            // --- 5️ 默认行为（非空即显示） ---
            else {
                result = value is not null;
            }

            // 应用参数前缀的反转逻辑
            if (negateParam)
                result = !result;

            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Visibility → bool 反向转换。
        /// 支持：
        /// - Visible → true；
        /// - Collapsed/Hidden → false；
        /// - “!” 前缀参数：逻辑取反；
        /// - Debug 标签：当参数中包含 "Debug" 且 DebugEnabled 为 true 时返回 true。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            string param = parameter as string ?? string.Empty;

            // “!” 前缀逻辑
            bool negateParam = false;
            if (!string.IsNullOrEmpty(param) && param.StartsWith('!')) {
                negateParam = true;
                param = param[1..];
            }

            // Debug 模式：直接返回 true
            if (DebugEnabled && param.Contains("Debug", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            bool result = value is Visibility vis && vis == Visibility.Visible;

            // 取反逻辑
            if (negateParam)
                result = !result;

            return result;
        }
    }
}
