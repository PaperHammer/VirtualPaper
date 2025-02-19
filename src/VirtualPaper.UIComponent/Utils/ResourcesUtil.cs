using System.Collections.Concurrent;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace VirtualPaper.UIComponent.Utils {
    public static class ResourcesUtil {
        public static SolidColorBrush GetBrush(string key) {
            if (_brushCache.TryGetValue(key, out var cachedBrush)) {
                return cachedBrush;
            }

            if (Application.Current.Resources.TryGetValue(key, out object resource) && resource is SolidColorBrush brush) {
                _brushCache[key] = brush;
                return brush;
            }

            var finalBrush = new SolidColorBrush(Colors.Transparent);
            _brushCache[key] = finalBrush;
            return finalBrush;
        }

        private static readonly ConcurrentDictionary<string, SolidColorBrush> _brushCache = [];
    }
}
