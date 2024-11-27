using CommandLine;
using VirtualPaper.Common;

namespace VirtualPaper.PlayerWeb {
    public class StartArgs {
        [Option("file-path",
            Required = true,
            Default = null,
            HelpText = "The target file to load.")]
        public string FilePath { get; set; }
        
        [Option("depth-file-path",
            Required = false,
            Default = null,
            HelpText = "The depth file created by ai to load .")]
        public string DepthFilePath { get; set; }

        [Option("effect-file-path-using",
               Required = true,
               Default = null,
               HelpText = "The wp-effect-file-path-using.")]
        public string WpEffectFilePathUsing { get; set; }
        
        [Option("effect-file-path-temporary",
               Required = true,
               Default = null,
               HelpText = "The wp-effect-file-path-temporary.")]
        public string WpEffectFilePathTemporary { get; set; }
        
        [Option("effect-file-path-template",
               Required = true,
               Default = null,
               HelpText = "The wp-effect-file-path-template.")]
        public string WpEffectFilePathTemplate { get; set; }

        [Option("runtime-type",
            Required = true,
            Default = null,
            HelpText = "The wallaper runtime type.")]
        public string RuntimeType { get; set; }

        [Option("is-preview",
            Required = true,
            Default = true,
            HelpText = "Is started for preview.")]
        public bool IsPreview { get; set; }
        
        [Option("window-style-type",
            Required = true,
            Default = default,
            HelpText = "window-style-type.")]
        public string WindowStyleType { get; set; }
        
        [Option("app-theme",
            Required = true,
            Default = AppTheme.Auto,
            HelpText = "application-theme.")]
        public AppTheme ApplicationTheme { get; set; }

        [Option("app-language",
            Required = true,
            Default = "zh-CN",
            HelpText = "app-language")]
        public string Language { get; set; }
    }
}
