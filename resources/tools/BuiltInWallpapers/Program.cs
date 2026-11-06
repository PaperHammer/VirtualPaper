using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.ML.DepthEstimate;
using VirtualPaper.Models.Cores;
using VirtualPaper.WpSettingsPanel.Utils;

internal static class Program {
    private sealed record Item(string Folder, string Source, FileType Type, string Title, string Description, string Tags,
        bool Depth = false, bool GeneratedImage = true, string Author = "VirtualPaper");
    private static readonly Item[] Items = [
        new("vp-alpine-stillness", "alpine-lake.png", FileType.FImage, "Alpine Stillness",
            "Morning light touches distant mountain peaks above a clear turquoise lake. Includes a prepared depth map for the 3D image option. AI-generated landscape, not a photograph of a real location.", "Landscape;Lake;Mountains;3D-ready", true),
        new("vp-emerald-morning", "emerald-ridges.png", FileType.FImage, "Emerald Morning",
            "Soft sunlight and low mist settle across rolling green hills. Includes a prepared depth map for the 3D image option. AI-generated landscape, not a photograph of a real location.", "Landscape;Hills;Morning;3D-ready", true),
        new("vp-bigora-cascade", "bigora-cascade.gif", FileType.FGif, "Bigora Cascade",
            "Water flows over moss-covered rock at Bigora waterfall. A repeating GIF adapted from real footage by Termininja, released under CC0 1.0. Free to reuse, modify and redistribute, including commercially.",
            "Landscape;Waterfall;Loop;Animation;CC0", GeneratedImage: false, Author: "Termininja"),
        new("vp-santa-rosa-waves", "santa-rosa-waves.mp4", FileType.FVideo, "Santa Rosa Waves",
            "Pacific waves meet the rugged coast of Santa Rosa Island, California. A silent, real-time 1080p recording adapted from footage by Blervis, released under CC0 1.0. Free to reuse, modify and redistribute, including commercially.",
            "Landscape;Ocean;Coast;Loop;Video;CC0", GeneratedImage: false, Author: "Blervis"),
        new("vp-luminous-flow", "luminous-flow.zip", FileType.FWebZip, "Luminous Flow",
            "Cyan and violet light streams follow the pointer and bloom on click, with curved trails over a dark background. An offline Web wallpaper inspired by VirtualPaper's Interactive Glow template, with pause and reduced-motion support.", "Abstract;Light;Interactive;Web", GeneratedImage: false)
    ];

    [STAThread]
    private static int Main(string[] args) {
        if (args.Length is < 1 or > 2) throw new ArgumentException("Usage: Generate <repository-root> [--verify]");
        if (args.Length == 2 && args[1] != "--verify") throw new ArgumentException("The only optional argument is --verify.");
        string root = Path.GetFullPath(args[0]);
        string core = Path.Combine(root, "src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0");
        AssemblyLoadContext.Default.Resolving += (_, name) => {
            foreach (string folder in new[] { core, Path.Combine(core, "Plugins/UI") }) {
                string file = Path.Combine(folder, name.Name + ".dll");
                if (File.Exists(file)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
            }
            return null;
        };
        NativeLibrary.SetDllImportResolver(typeof(Cv2).Assembly, (name, _, _) =>
            name == "OpenCvSharpExtern" ? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "OpenCvSharpExtern.dll")) : IntPtr.Zero);
        NativeLibrary.SetDllImportResolver(typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly, (name, _, _) =>
            name == "onnxruntime" ? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "onnxruntime.dll")) : IntPtr.Zero);
        // Keep logging and any helper temp files away from the daily application profile.
        Environment.SetEnvironmentVariable("VIRTUALPAPER_TEST_DATA_ROOT", Path.Combine(root, "TestResults/built-in-wallpapers/profile"));
        Run(root, args.Length == 2 && args[1] == "--verify");
        return 0;
    }

    private static void Run(string root, bool verifyOnly) {
        string staging = Path.Combine(root, "TestResults/built-in-wallpapers");
        string destination = Path.Combine(root, "resources/BuiltInWallpapers");
        var coreAssembly = typeof(VirtualPaper.Utils.WallpaperUtil).Assembly;
        foreach (var item in Items) {
            string folder = Path.GetFullPath(Path.Combine(destination, item.Folder));
            if (!verifyOnly) {
                Directory.CreateDirectory(folder);
                var data = new WpBasicData {
                    WallpaperUid = "LCL" + item.Folder, FolderName = item.Folder, FolderPath = folder,
                    FType = item.Type, AppInfo = new() { AppName = "Virtual Paper", AppVersion = coreAssembly.GetName().Version!.ToString(), FileVersion = Constants.CoreField.FileVersion },
                    CreatedTime = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc),
                    PublishDate = "2026-11-06", IsSubscribed = true, Partition = "Built-in",
                };
                // Use precisely the builders used by WallpaperControl.CreateBasicData.
                // Reflection keeps this offline authoring tool out of Core's public API.
                string builderName = item.Type switch {
                    FileType.FImage or FileType.FGif => "ImageBasicDataBuilder",
                    FileType.FVideo => "VideoBasicDataBuilder",
                    FileType.FWebZip => "WebZipBasicDataBuilder",
                    _ => throw new NotSupportedException()
                };
                var builderType = coreAssembly.GetType("VirtualPaper.Utils.BasicDataBuilders." + builderName, throwOnError: true)!;
                var builder = Activator.CreateInstance(builderType, nonPublic: true)!;
                builderType.GetMethod("Build")!.Invoke(builder, [Path.Combine(staging, item.Source), folder, item.Folder, data, CancellationToken.None]);
                // The default thumbnail builder favors large previews. Ship compact
                // versions for these bundled items without changing their original media.
                File.Copy(Path.Combine(staging, item.Folder + "-thumbnail.gif"), data.ThumbnailPath, overwrite: true);
                data.Title = item.Title; data.Desc = item.Description; data.Authors = item.Author;
                data.Tags = "Built-in;内置;" + (item.GeneratedImage ? "AI-generated;" : "") + item.Tags;
                data.SaveAsync().GetAwaiter().GetResult();
                WriteNotice(root, folder, item);
                if (item.Depth) {
                    string model = Path.Combine(root, "src/VirtualPaper.ML/DepthEstimate/ai_models/depth_anything_v2_vits_dynamic.onnx");
                    string depth = DepthMapCache.GetOrCreateAsync(data.FilePath, folder, () => new DepthAnythingV2(4), CancellationToken.None, model).GetAwaiter().GetResult();
                    Console.WriteLine($"Prepared depth cache: {Path.GetFileName(depth)}");
                }
                Console.WriteLine($"Packaged {item.Title}: {data.Resolution}");
            }
            VerifyItem(folder, item);
        }
        var index = new WallpaperIndexFile();
        index.RebuildIndexAsync([destination]).GetAwaiter().GetResult();
        if (index.Items.Count != Items.Length) throw new InvalidDataException("Not all built-ins were indexed.");
        Console.WriteLine($"Verified {index.Items.Count} built-in wallpapers using the production library scanner.");
        Console.WriteLine($"Total payload: {Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) / 1048576.0:F2} MiB");
    }

    private static void WriteNotice(string root, string folder, Item item) {
        if (item.Author != "VirtualPaper") {
            string tools = Path.Combine(root, "resources/tools/BuiltInWallpapers");
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(tools, "media.json")));
            var source = manifest.RootElement.GetProperty("media").EnumerateArray().Single(x => x.GetProperty("folder").GetString() == item.Folder);
            if (source.GetProperty("author").GetString() != item.Author) throw new InvalidDataException("Source author mismatch.");
            File.Copy(Path.Combine(tools, "CC0-1.0.txt"), Path.Combine(folder, "CC0-1.0.txt"), overwrite: true);
            File.WriteAllText(Path.Combine(folder, "NOTICE.txt"),
                $"{item.Title}\nOriginal footage: {source.GetProperty("originalTitle").GetString()}\n" +
                $"Original creator: {item.Author}\nSource: {source.GetProperty("sourceUrl").GetString()}\n" +
                $"License evidence: {source.GetProperty("sourceRevisionUrl").GetString()}\n" +
                $"Original download: {source.GetProperty("downloadUrl").GetString()}\n" +
                $"Original SHA-256: {source.GetProperty("sha256").GetString()}\n" +
                "The creator identifies this as their own work and dedicates it under CC0 1.0 Universal. Verified 2026-11-06.\n" +
                "License: https://creativecommons.org/publicdomain/zero/1.0/ (full text in CC0-1.0.txt).\n" +
                "You may use, copy, modify and redistribute this footage, including as standalone media and for commercial purposes, without attribution or share-alike requirements.\n" +
                $"Adaptation by VirtualPaper: {source.GetProperty("processing").GetString()}\n" +
                "VirtualPaper also dedicates any copyright it holds in these media adaptations and thumbnails under CC0 1.0. No additional media-use restrictions are imposed by this application or its code license.\n" +
                "Creator and source are recorded for provenance, not as an attribution condition. No endorsement by the creator is implied.\n" +
                "CC0 waives copyright and related rights to the extent legally possible; it does not clear third-party trademark, privacy or other rights, and provides no warranties.\n");
            return;
        }
        File.WriteAllText(Path.Combine(folder, "NOTICE.txt"),
                    "Creator / publisher: VirtualPaper.\n" +
                    (item.GeneratedImage ? "The landscape imagery was created with OpenAI image generation for this collection. It is synthetic imagery, not a photograph of a real location.\n" : "Original Canvas animation by VirtualPaper, inspired by the project's Interactive Glow Web template. No photographs are included.\n") +
                    "No third-party stock photographs, fonts, music or textures were intentionally incorporated.\n" +
                    "VirtualPaper provides its animation code under GNU GPL version 3, matching the repository.\n" +
                    (item.GeneratedImage ? "To the extent VirtualPaper holds rights in the generated imagery, it permits its use, modification and redistribution with this notice. No exclusive copyright is claimed in AI-generated imagery.\n" : "") +
                    "Source and generation instructions: https://github.com/PaperHammer/VirtualPaper/tree/main/resources/tools/BuiltInWallpapers\n" +
                    (item.Depth ? "The depth map was generated locally with VirtualPaper's Depth Anything V2 pipeline. The model is not part of this wallpaper folder.\n" : ""));
    }

    private static void VerifyItem(string folder, Item item) {
        string metadataPath = Path.Combine(folder, Constants.Field.WpBasicDataFileName);
        var loaded = WpBasicData.Load(metadataPath);
        if (!loaded.IsAvailable() || !File.Exists(loaded.FilePath) || !File.Exists(loaded.ThumbnailPath))
            throw new InvalidDataException($"Invalid packaged wallpaper: {item.Title}");
        if (loaded.Title != item.Title || loaded.Authors != item.Author || !loaded.Tags.Split(';').Contains("内置") || string.IsNullOrWhiteSpace(loaded.Desc))
            throw new InvalidDataException($"Incomplete metadata: {item.Title}");
        if (item.Author != "VirtualPaper" && (!File.Exists(Path.Combine(folder, "CC0-1.0.txt")) || loaded.Tags.Split(';').Contains("AI-generated")))
            throw new InvalidDataException($"Incorrect external media license/tags: {item.Title}");
        using var json = JsonDocument.Parse(File.ReadAllText(metadataPath));
        foreach (string name in new[] { "FolderPath", "FilePath", "ThumbnailPath" }) {
            string path = json.RootElement.GetProperty(name).GetString()!;
            if (Path.IsPathRooted(path)) throw new InvalidDataException($"Nonportable {name}: {item.Title}");
            string resolved = Path.GetFullPath(path, folder);
            if (!resolved.Equals(folder, StringComparison.OrdinalIgnoreCase) && !resolved.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"External reference: {item.Title}");
        }
        if (item.Depth) {
            string depth = Directory.EnumerateFiles(folder, "depth-*.png").Single();
            using var mat = Cv2.ImRead(depth, ImreadModes.Grayscale);
            if (mat.Empty()) throw new InvalidDataException("Invalid depth map.");
            Cv2.MinMaxLoc(mat, out double minimum, out double maximum);
            if (maximum - minimum < 32) throw new InvalidDataException("Depth map has insufficient range.");
            // Calling the cache with a failing factory proves this package avoids inference.
            string model = Path.GetFullPath(Path.Combine(folder, "../../../src/VirtualPaper.ML/DepthEstimate/ai_models/depth_anything_v2_vits_dynamic.onnx"));
            string reused = DepthMapCache.GetOrCreateAsync(loaded.FilePath, folder,
                () => throw new InvalidOperationException("Bundled depth cache was not reused."), CancellationToken.None, model).GetAwaiter().GetResult();
            if (reused != depth) throw new InvalidDataException("Depth cache key mismatch.");
        }
    }
}
