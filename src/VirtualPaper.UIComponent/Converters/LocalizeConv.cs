using System;
using Microsoft.UI.Xaml.Data;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Converters {
    public partial class LocalizeConv : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value is not string key) return null;

            if (string.IsNullOrEmpty(key)) return key;

            return LanguageUtil.GetI18n(key);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
