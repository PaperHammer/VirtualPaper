using Microsoft.UI.Xaml;
using VirtualPaper.Common;

namespace VirtualPaper.UIComponent.Utils.Extensions {
    public static class ThemeExtensions {
        public static AppTheme ToAppTheme(this ElementTheme et) {
            return et switch {
                ElementTheme.Dark => AppTheme.Dark,
                ElementTheme.Light => AppTheme.Light,
                _ => AppTheme.Auto,
            };
        }

        public static ElementTheme ToElementTheme(this AppTheme at) {
            return at switch {
                AppTheme.Dark => ElementTheme.Dark,
                AppTheme.Light => ElementTheme.Light,
                _ => ElementTheme.Default,
            };
        }       
    }
}
