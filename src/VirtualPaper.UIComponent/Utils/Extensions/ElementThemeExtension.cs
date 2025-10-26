using Microsoft.UI.Xaml;
using VirtualPaper.Common;

namespace VirtualPaper.UIComponent.Utils.Extensions {
    public static class ElementThemeExtension {
        public static AppTheme ToAppTheme(this ElementTheme et) {
            return et switch {
                ElementTheme.Dark => AppTheme.Dark,
                ElementTheme.Light => AppTheme.Light,
                _ => AppTheme.Auto,
            };
        }
    }
}
