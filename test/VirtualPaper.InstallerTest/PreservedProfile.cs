using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Grpc.Service.UserSettings;
using VirtualPaper.Grpc.Service.WallpaperControl;

namespace VirtualPaper.InstallerTest;

internal sealed class PreservedProfile {
    public string Language { get; set; } = "";
    public string WallpaperDir { get; set; } = "";
    public string WallpaperUid { get; set; } = "";
    public Dictionary<string, string> FileHashes { get; set; } = [];

    public static string BaselinePath(string root) => Path.Combine(root, "reinstall-baseline.json");

    public static PreservedProfile Prepare(Grpc_UserSettingsService.Grpc_UserSettingsServiceClient settings) {
        var saved = settings.GetSettings(new Empty(), deadline: DateTime.UtcNow.AddSeconds(5));
        saved.ApplicationTheme = (Grpc_AppTheme)AppTheme.Dark;
        saved.NavigationAnimationsEnabled = false;
        settings.SetSettings(saved, deadline: DateTime.UtcNow.AddSeconds(5));
        string source = Path.Combine(Constants.CommonPaths.AppDataDir, "reinstall-source.png");
        byte[] pixels = new byte[64 * 64 * 4];
        for (int i = 0; i < pixels.Length; i += 4) {
            pixels[i] = (byte)(i / 4 % 256); pixels[i + 1] = 120; pixels[i + 2] = 200; pixels[i + 3] = 255;
        }
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, pixels, 256)));
        using (var stream = File.Create(source)) encoder.Save(stream);
        var wallpapers = new Grpc_WallpaperControlService.Grpc_WallpaperControlServiceClient(
            new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));
        var wallpaper = wallpapers.CreateMetadataBasic(new() { FilePath = source, FType = (Grpc_FileType)FileType.FImage },
            deadline: DateTime.UtcNow.AddSeconds(15));
        if (string.IsNullOrEmpty(wallpaper.WallpaperUid) || !File.Exists(wallpaper.FilePath) || !File.Exists(wallpaper.ThumbnailPath))
            throw new InvalidOperationException("The persistence fixture was not imported by Core.");
        return new() { Language = saved.Language, WallpaperDir = saved.WallpaperDir, WallpaperUid = wallpaper.WallpaperUid };
    }

    public void SaveBaseline(string root) {
        VerifySettings();
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(WallpaperDir).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The fixture library must remain inside the isolated profile.");
        foreach (string file in Directory.EnumerateFiles(WallpaperDir, "*", SearchOption.AllDirectories))
            FileHashes.Add(Path.GetRelativePath(root, file), Hash(file));
        if (FileHashes.Count < 3) throw new InvalidOperationException("The persistence fixture is incomplete.");
        File.WriteAllText(BaselinePath(root), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static PreservedProfile Load(string root) => JsonSerializer.Deserialize<PreservedProfile>(File.ReadAllText(BaselinePath(root)))
        ?? throw new InvalidDataException("The reinstall baseline is empty.");

    public void VerifySettings(Grpc_SettingsData? live = null) {
        using var saved = JsonDocument.Parse(File.ReadAllText(Constants.CommonPaths.UserSettingsPath));
        var json = saved.RootElement;
        if (json.GetProperty("Language").GetString() != Language || json.GetProperty("WallpaperDir").GetString() != WallpaperDir ||
            json.GetProperty("ApplicationTheme").GetString() != AppTheme.Dark.ToString() || json.GetProperty("NavigationAnimationsEnabled").GetBoolean())
            throw new InvalidOperationException("Saved language, theme, animation preference or library directory changed during reinstall.");
        if (live != null && (live.Language != Language || live.WallpaperDir != WallpaperDir ||
            (int)live.ApplicationTheme != (int)AppTheme.Dark || live.NavigationAnimationsEnabled))
            throw new InvalidOperationException("Core did not retain the saved profile after reinstall.");
    }

    public void VerifyFiles(string root) {
        if (FileHashes.Count < 3) throw new InvalidDataException("The reinstall baseline contains no complete wallpaper.");
        foreach (var (relative, expected) in FileHashes) {
            string file = Path.GetFullPath(relative, root);
            if (!File.Exists(file) || Hash(file) != expected)
                throw new InvalidOperationException($"Reinstall changed a wallpaper file: {relative}");
        }
    }

    private static string Hash(string file) {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
