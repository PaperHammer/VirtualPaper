using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace VirtualPaper.UI.Utils.Converters {
    internal class ThumbnailPathConvert : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            string filePath = value.ToString();
            if (!File.Exists(filePath)) return null;
            
            return new Uri(filePath);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
