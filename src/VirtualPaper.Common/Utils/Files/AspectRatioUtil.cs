using System.Globalization;
using System.Text.RegularExpressions;

namespace VirtualPaper.Common.Utils.Files;

public static class AspectRatioUtil {
    private static readonly (int Width, int Height)[] CommonRatios = [
        (1, 1), (5, 4), (4, 3), (3, 2), (16, 10), (16, 9), (21, 9), (32, 9)
    ];

    public static string Format(int width, int height) {
        if (width <= 0 || height <= 0) return "-";
        // Retain the familiar labels for standard formats such as 1366 x 768.
        double ratio = (double)width / height;
        if (Math.Abs(ratio - 1.6) < 0.01) return "16:10";
        if (Math.Abs(ratio - 1.7778) < 0.01) return "16:9";
        if (Math.Abs(ratio - 1.3333) < 0.01) return "4:3";
        int a = width, b = height;
        while (b != 0) (a, b) = (b, a % b);
        return $"{width / a}:{height / a}";
    }

    public static string Format(string? aspectRatio, string? resolution) {
        var stored = Regex.Match(aspectRatio ?? "", @"^([1-9][0-9]*)\s*:\s*([1-9][0-9]*)$",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (stored.Success
            && int.TryParse(stored.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int storedWidth)
            && int.TryParse(stored.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int storedHeight))
            return Describe(aspectRatio!, storedWidth, storedHeight);
        // Legacy metadata can contain enum names such as FUnknown. Use stored
        // dimensions without opening media or rewriting metadata just to display it.
        var dimensions = Regex.Match(resolution ?? "", @"^\s*(\d+)\s*[*×xX]\s*(\d+)(?:\s|\(|$)",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return dimensions.Success
            && int.TryParse(dimensions.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int width)
            && int.TryParse(dimensions.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int height)
            ? Describe(Format(width, height), width, height) : "-";
    }

    // The hint belongs to display text only; stored metadata keeps its plain ratio.
    private static string Describe(string exact, int width, int height) {
        if (width <= 0 || height <= 0) return "-";
        bool portrait = width < height;
        double ratio = portrait ? (double)height / width : (double)width / height;
        var nearest = CommonRatios.MinBy(common => Math.Abs(ratio - (double)common.Width / common.Height));
        string label = portrait ? $"{nearest.Height}:{nearest.Width}" : $"{nearest.Width}:{nearest.Height}";
        return Math.Abs(ratio - (double)nearest.Width / nearest.Height) < 0.01
            ? label : $"{exact} (≈ {label})";
    }
}
