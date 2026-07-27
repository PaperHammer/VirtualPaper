using System;
using System.IO;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using Workloads.Creation.WebBackdrop.Core.Theme;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace Workloads.Creation.WebBackdrop.Core.Preview {
    public readonly record struct MarkdownPreviewTheme(
        string PreviewBackground,
        string PreviewForeground,
        string PreviewSecondaryForeground,
        string PreviewCodeBackground,
        string PreviewQuoteBorder,
        string PreviewLinkForeground) {
        public static MarkdownPreviewTheme FromElement(FrameworkElement element) {
            var isLightTheme = element.ActualTheme == ElementTheme.Light;
            return new MarkdownPreviewTheme(
                GetPreviewString(element, isLightTheme, WebBackdropStringRole.PreviewLightBackground, WebBackdropStringRole.PreviewDarkBackground),
                GetPreviewString(element, isLightTheme, WebBackdropStringRole.PreviewLightForeground, WebBackdropStringRole.PreviewDarkForeground),
                GetPreviewString(element, isLightTheme, WebBackdropStringRole.PreviewLightSecondaryForeground, WebBackdropStringRole.PreviewDarkSecondaryForeground),
                GetPreviewString(element, isLightTheme, WebBackdropStringRole.PreviewLightCodeBackground, WebBackdropStringRole.PreviewDarkCodeBackground),
                GetPreviewString(element, isLightTheme, WebBackdropStringRole.PreviewLightQuoteBorder, WebBackdropStringRole.PreviewDarkQuoteBorder),
                GetPreviewString(element, isLightTheme, WebBackdropStringRole.PreviewLightLinkForeground, WebBackdropStringRole.PreviewDarkLinkForeground));
        }

        private static string GetPreviewString(FrameworkElement element, bool isLightTheme, WebBackdropStringRole lightRole, WebBackdropStringRole darkRole) {
            return WebBackdropThemeResource.GetString(element, isLightTheme ? lightRole : darkRole);
        }
    }

    public static class MarkdownPreviewRenderer {
        private static readonly Lazy<string> TemplateHtml = new(() => File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, Constants.ModuleName.WebBackdrop, "Assets", "preview", "markdown-preview.html")));

        public static string Render(string markdown, MarkdownPreviewTheme theme) {
            return TemplateHtml.Value
                .Replace("{{PreviewBackground}}", theme.PreviewBackground)
                .Replace("{{PreviewForeground}}", theme.PreviewForeground)
                .Replace("{{PreviewSecondaryForeground}}", theme.PreviewSecondaryForeground)
                .Replace("{{PreviewCodeBackground}}", theme.PreviewCodeBackground)
                .Replace("{{PreviewQuoteBorder}}", theme.PreviewQuoteBorder)
                .Replace("{{PreviewLinkForeground}}", theme.PreviewLinkForeground)
                .Replace("{{MarkdownHtml}}", RenderBody(markdown));
        }

        public static string RenderBody(string markdown) {
            return WebEditorFileUtil.RenderMarkdown(markdown);
        }
    }
}
