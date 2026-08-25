using System;
using System.Collections.Concurrent;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace VirtualPaper.UIComponent.Converters {
    /// <summary>
    /// 将资源字典中的 Geometry 路径字符串转换为当前可视元素独享的 Geometry。
    /// 只缓存不可变的字符串数据，避免 TreeView 容器回收后复用 WinRT DependencyObject。
    /// </summary>
    public partial class ResourceKeyToGeometryConverter : IValueConverter {
        public object? Convert(object value, Type targetType, object parameter, string language) {
            if (value is not string { Length: > 0 } resourceKey) return null;

            if (!_pathDataCache.TryGetValue(resourceKey, out var pathData)) {
                if (!Application.Current.Resources.TryGetValue(resourceKey, out var resource)
                    || resource is not string { Length: > 0 } resourcePathData) {
                    return null;
                }

                pathData = _pathDataCache.GetOrAdd(resourceKey, resourcePathData);
            }

            return XamlBindingHelper.ConvertValue(typeof(Geometry), pathData) as Geometry;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotSupportedException();
        }

        private static readonly ConcurrentDictionary<string, string> _pathDataCache = new();
    }
}
