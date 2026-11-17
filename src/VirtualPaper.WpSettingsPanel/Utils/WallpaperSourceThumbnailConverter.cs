using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace VirtualPaper.WpSettingsPanel.Utils;

// The picker owns only realized images; never decode an oversized preview at its original size.
public sealed partial class WallpaperSourceThumbnailConverter : IValueConverter {
    public object? Convert(object value, Type targetType, object parameter, string language) {
        if (value is not string path || !Uri.TryCreate(path, UriKind.Absolute, out var uri)) return null;
        return new BitmapImage { DecodePixelWidth = 144, UriSource = uri };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
