namespace VirtualPaper.Common.Runtime.Draft {
    public record ExportDataStaticImg(string Name, string Path, ExportImageFormat Format, double ScalePercentage, float? JpegQuality = null);

    public enum ExportImageFormat {
        Png,
        Bmp,
        Jpeg,
        JpegXR,
    }
}
