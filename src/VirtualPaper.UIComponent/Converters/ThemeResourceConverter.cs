using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Converters {
    public partial class ThemeResourceConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value is string key && Application.Current.Resources.TryGetValue(key, out var resource)) {
                // 如果资源存在，则返回它
                return resource;
            }
            // 如果没有找到资源，则返回 null 或者可以设置一个默认值
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
