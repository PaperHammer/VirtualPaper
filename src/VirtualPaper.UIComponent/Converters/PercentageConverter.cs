using System;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Converters {
    public partial class PercentageConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value is double v && parameter is string percentageString && double.TryParse(percentageString, out double percentage)) {
                return v * percentage;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
