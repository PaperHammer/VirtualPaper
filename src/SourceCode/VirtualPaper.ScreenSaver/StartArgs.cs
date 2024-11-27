using CommandLine;

namespace VirtualPaper.ScreenSaver {
    public class StartArgs {
        [Option("file-path",
            Required = true,
            HelpText = "The target-file to load.")]
        public string FilePath { get; set; } = string.Empty;

        [Option("wallpaper-type",
            Required = true,
            Default = null,
            HelpText = "The wallaper type.")]
        public string WallpaperType { get; set; } = string.Empty;

        [Option("effect",
            Required = true,
            Default = "none",
            HelpText = "The dynamic effect.")]
        public string DynamicEffect { get; set; } = string.Empty;
    }
}
