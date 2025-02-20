using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Converters {
    public partial class DecimalPlaceConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value is double num) {
                return num.ToString("0.0"); // 保留一位小数
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            if (value is string strValue && double.TryParse(strValue, out double result)) {
                return result;
            }
            return DependencyProperty.UnsetValue;
        }
    }
}
