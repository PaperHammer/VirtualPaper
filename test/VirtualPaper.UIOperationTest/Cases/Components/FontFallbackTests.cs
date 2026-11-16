using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class FontFallbackTests {
    private static readonly (string Language, string Text)[] Samples = [
        ("zh-CN", "山水壁纸 · 作者：VirtualPaper · 简体中文"),
        ("zh-TW", "山水桌布 · 作者：VirtualPaper · 繁體中文"),
        ("ja-JP", "風景の壁紙 · 作者：VirtualPaper · 日本語"),
        ("ko-KR", "풍경 배경 화면 · 작성자: VirtualPaper · 한국어"),
        ("ru-RU", "Пейзаж · Автор: VirtualPaper · Кириллица"),
        ("fr-FR", "Forêt, lumière, été · Straße · ação · español"),
        ("en-US", "Mixed: 山 / 森 / 숲 / Лес · ①②③ · © ® ™ € £ ¥")
    ];

    public static async Task RunAsync(UiTestContext context) {
        var page = await context.OpenAsync<LocalizationTestPage>(new());
        await context.ResizeAsync(1150, 850);
        var panel = new StackPanel { Padding = new Thickness(24), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Font fallback: text, editable input and read-only input", FontSize = 22 });
        var rows = new List<(TextBlock Label, TextBox Input, TextBox ReadOnly)>();
        foreach (var (language, text) in Samples) {
            var row = new Grid { ColumnSpacing = 16 };
            for (int i = 0; i < 3; i++) row.ColumnDefinitions.Add(new ColumnDefinition());
            var label = new TextBlock { Text = text, Language = language, FontSize = 16, TextWrapping = TextWrapping.Wrap };
            var input = new TextBox { Text = text, Language = language, FontSize = 16, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
            var readOnly = new TextBox { Text = text, Language = language, FontSize = 16, IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
            row.Children.Add(label); row.Children.Add(input); row.Children.Add(readOnly);
            Grid.SetColumn(input, 1); Grid.SetColumn(readOnly, 2);
            panel.Children.Add(row);
            rows.Add((label, input, readOnly));
        }
        page.TestSurface.Child = panel;
        var screenshots = new List<string>();
        foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark }) {
            page.RequestedTheme = theme;
            await context.WaitForLayoutAsync();
            foreach (var (label, input, readOnly) in rows) {
                Check(!label.IsTextTrimmed && label.ActualHeight > 0 && input.Text == label.Text && readOnly.Text == label.Text,
                    "Mixed-script text was trimmed, lost or changed in an input");
                var bottom = readOnly.TransformToVisual(page).TransformPoint(new(0, readOnly.ActualHeight));
                Check(bottom.Y <= page.ActualHeight, "Font fallback fixture overflowed the available height");
            }
            string path = Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", $"font-fallback-{theme}.png");
            await SaveRenderedPageAsync(page, path);
            Check(File.Exists(path) && new FileInfo(path).Length > 0, "Font fallback screenshot was not captured");
            screenshots.Add(path);
        }
        context.Evidence["MixedScriptLanguages"] = Samples.Select(item => item.Language).ToArray();
        context.Evidence["FontFallbackScreenshots"] = screenshots;
        context.Evidence["FontFallbackScope"] = "Rendered production text/input styles; glyph appearance requires screenshot review on each target Windows image.";
    }

    private static async Task SaveRenderedPageAsync(FrameworkElement page, string path) {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(page);
        var pixels = await bitmap.GetPixelsAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path));
        var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.FailIfExists);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
}
