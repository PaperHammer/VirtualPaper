using CommandLine;
using VirtualPaper.Common;

namespace VirtualPaper.PlayerWeb {
    public class StartArgs {
        [Option("left",
            Required = false,
            Default = 0,
            HelpText = "The target monitor workingArea point for left.")]
        public int Left { get; set; }
        
        [Option("top",
            Required = false,
            Default = 0,
            HelpText = "The target monitor workingArea point for top.")]
        public int Top { get; set; }
        
        [Option("right",
            Required = false,
            Default = 0,
            HelpText = "The target monitor workingArea point for right.")]
        public int Right { get; set; }
        
        [Option("bottom",
            Required = false,
            Default = 0,
            HelpText = "The target monitor workingArea point for bottom.")]
        public int Bottom { get; set; }
        
        [Option('f', "file-path",
            Required = true,
            HelpText = "The target file to load.")]
        public string FilePath { get; set; }

        [Option("depth-file-path",
            Required = false,
            Default = null,
            HelpText = "The depth file created by ai to load .")]
        public string DepthFilePath { get; set; }

        [Option('b', "basic-data-file-path",
            Required = true,
            HelpText = "The wp-basic-data-file-path.")]
        public string WpBasicDataFilePath { get; set; }

        [Option('e', "effect-file-path-using",
            Required = true,
            HelpText = "The wp-effect-file-path-using.")]
        public string WpEffectFilePathUsing { get; set; }

        [Option("effect-file-path-temporary",
            Required = true,
            HelpText = "The wp-effect-file-path-temporary.")]
        public string WpEffectFilePathTemporary { get; set; }

        [Option("effect-file-path-template",
            Required = true,
            HelpText = "The wp-effect-file-path-template.")]
        public string WpEffectFilePathTemplate { get; set; }

        [Option('r', "runtime-type",
            Required = true,
            HelpText = "The wallaper runtime type.")]
        public string RuntimeType { get; set; }

        [Option("is-preview",
            Required = false,
            HelpText = "Is started for preview.")]
        public bool IsPreview { get; set; }

        //[Option("system-backdrop",
        //    Required = true,
        //    HelpText = "system-backdrop.")]
        //public AppSystemBackdrop SystemBackdrop { get; set; }

        //[Option('t', "app-theme",
        //    Required = true,
        //    HelpText = "application-theme.")]
        //public AppTheme ApplicationTheme { get; set; }

        //[Option('l', "app-language",
        //    Required = true,
        //    HelpText = "app-language")]
        //public string Language { get; set; }
    }
}
