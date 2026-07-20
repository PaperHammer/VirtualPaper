using Microsoft.UI.Xaml;
using Windows.UI;

namespace Workloads.Creation.WebBackdrop.Core.Theme {
    public enum WebBackdropBrushRole {
        ProblemError,
        ProblemWarning,
        ProblemInactive,
        ProblemBadgeBackground,
        ProblemBadgeForeground,
        SplitterHover,
    }

    public enum WebBackdropColorRole {
        WebViewLightBackground,
        WebViewDarkBackground,
    }

    public enum WebBackdropStringRole {
        PreviewLightBackground,
        PreviewDarkBackground,
        PreviewLightForeground,
        PreviewDarkForeground,
        PreviewLightSecondaryForeground,
        PreviewDarkSecondaryForeground,
        PreviewLightCodeBackground,
        PreviewDarkCodeBackground,
        PreviewLightQuoteBorder,
        PreviewDarkQuoteBorder,
        PreviewLightLinkForeground,
        PreviewDarkLinkForeground,
        MonacoFallbackLightBackground,
        MonacoFallbackDarkBackground,
        MonacoFallbackLightForeground,
        MonacoFallbackDarkForeground,
    }

    public static class WebBackdropThemeResource {
        public static string GetBrushKey(WebBackdropBrushRole role) {
            return role switch {
                WebBackdropBrushRole.ProblemError => "WebBackdropProblemErrorBrush",
                WebBackdropBrushRole.ProblemWarning => "WebBackdropProblemWarningBrush",
                WebBackdropBrushRole.ProblemInactive => "WebBackdropProblemInactiveBrush",
                WebBackdropBrushRole.ProblemBadgeBackground => "WebBackdropProblemBadgeBackgroundBrush",
                WebBackdropBrushRole.ProblemBadgeForeground => "WebBackdropProblemBadgeForegroundBrush",
                WebBackdropBrushRole.SplitterHover => "WebBackdropSplitterHoverBrush",
                _ => "TextFillColorPrimaryBrush",
            };
        }

        public static string GetColorKey(WebBackdropColorRole role) {
            return role switch {
                WebBackdropColorRole.WebViewLightBackground => "WebBackdropWebViewLightBackgroundColor",
                WebBackdropColorRole.WebViewDarkBackground => "WebBackdropWebViewDarkBackgroundColor",
                _ => "WebBackdropWebViewDarkBackgroundColor",
            };
        }

        public static string GetStringKey(WebBackdropStringRole role) {
            return role switch {
                WebBackdropStringRole.PreviewLightBackground => "WebBackdropPreviewLightBackground",
                WebBackdropStringRole.PreviewDarkBackground => "WebBackdropPreviewDarkBackground",
                WebBackdropStringRole.PreviewLightForeground => "WebBackdropPreviewLightForeground",
                WebBackdropStringRole.PreviewDarkForeground => "WebBackdropPreviewDarkForeground",
                WebBackdropStringRole.PreviewLightSecondaryForeground => "WebBackdropPreviewLightSecondaryForeground",
                WebBackdropStringRole.PreviewDarkSecondaryForeground => "WebBackdropPreviewDarkSecondaryForeground",
                WebBackdropStringRole.PreviewLightCodeBackground => "WebBackdropPreviewLightCodeBackground",
                WebBackdropStringRole.PreviewDarkCodeBackground => "WebBackdropPreviewDarkCodeBackground",
                WebBackdropStringRole.PreviewLightQuoteBorder => "WebBackdropPreviewLightQuoteBorder",
                WebBackdropStringRole.PreviewDarkQuoteBorder => "WebBackdropPreviewDarkQuoteBorder",
                WebBackdropStringRole.PreviewLightLinkForeground => "WebBackdropPreviewLightLinkForeground",
                WebBackdropStringRole.PreviewDarkLinkForeground => "WebBackdropPreviewDarkLinkForeground",
                WebBackdropStringRole.MonacoFallbackLightBackground => "WebBackdropMonacoFallbackLightBackground",
                WebBackdropStringRole.MonacoFallbackDarkBackground => "WebBackdropMonacoFallbackDarkBackground",
                WebBackdropStringRole.MonacoFallbackLightForeground => "WebBackdropMonacoFallbackLightForeground",
                WebBackdropStringRole.MonacoFallbackDarkForeground => "WebBackdropMonacoFallbackDarkForeground",
                _ => "WebBackdropPreviewDarkBackground",
            };
        }
        public static Color GetColor(FrameworkElement element, WebBackdropColorRole role) {
            return (Color)element.Resources[GetColorKey(role)];
        }

        public static string GetString(FrameworkElement element, WebBackdropStringRole role) {
            return (string)element.Resources[GetStringKey(role)];
        }
    }
}
