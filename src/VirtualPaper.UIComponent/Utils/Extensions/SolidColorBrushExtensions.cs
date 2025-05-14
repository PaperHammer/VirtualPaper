using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Media;

namespace VirtualPaper.UIComponent.Utils.Extensions {
    public static class SolidColorBrushExtensions {
        public static bool ContainsColor(this IEnumerable<SolidColorBrush> brushes, SolidColorBrush brush) {
            return brushes.Any(b => _comparer.Equals(b, brush));
        }

        public static SolidColorBrush FindBrushByColor(this IEnumerable<SolidColorBrush> brushes, SolidColorBrush brush) {
            return brushes.FirstOrDefault(b => _comparer.Equals(b, brush));
        }

        private static readonly SolidColorBrushComparer _comparer = new();
    }

    public class SolidColorBrushComparer : IEqualityComparer<SolidColorBrush> {
        public bool Equals(SolidColorBrush x, SolidColorBrush y) {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;

            return x.Color.Equals(y.Color);
        }

        public int GetHashCode(SolidColorBrush obj) {
            return obj?.Color.GetHashCode() ?? 0;
        }
    }
}
