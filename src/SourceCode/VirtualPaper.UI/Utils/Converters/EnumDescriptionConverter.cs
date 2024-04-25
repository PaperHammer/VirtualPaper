using Microsoft.UI.Xaml.Data;
using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace VirtualPaper.UI.Utils.Converters
{
    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            FieldInfo fieldInfo = value.GetType().GetField(value.ToString());
            if (fieldInfo != null)
            {
                var attribute = fieldInfo.GetCustomAttribute<EnumMemberAttribute>();
                return attribute?.Value ?? value.ToString();
            }
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
