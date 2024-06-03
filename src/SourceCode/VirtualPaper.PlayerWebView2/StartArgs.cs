using CommandLine;

namespace VirtualPaper.PlayerWebView2
{
    public class StartArgs
    {
        [Option("working-dir",
            Required = true,
            HelpText = "The program working directory.")]
        public string WorkingDir { get; set; } = string.Empty;

        [Option("file-path",
            Required = true,
            HelpText = "The target-file to load.")]
        public string FilePath { get; set; } = string.Empty;

        [Option("wallpaper-type",
            Required = true,
            Default = null,
            HelpText = "The wallaper type.")]
        public string WallpaperType { get; set; } = string.Empty;

        [Option("customize-file-path",
            Required = true,
            Default = null,
            HelpText = "WpCustomize filepath.")]
        public string WpCustomizeFilePath { get; set; } = string.Empty;
    }
}
