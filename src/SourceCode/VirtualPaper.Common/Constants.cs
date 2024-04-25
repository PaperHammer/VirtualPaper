namespace VirtualPaper.Common
{
    public static class Constants
    {
        public static class CommonPaths
        {
            /// <summary>
            /// 数据存储根目录
            /// </summary>
            public static string AppDataDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualPaper");
            
            /// <summary>
            /// 日志
            /// </summary>
            public static string LogDir { get; } = Path.Combine(AppDataDir, "logs");
            public static string LogDirCore { get; } = Path.Combine(LogDir, "Core");
            public static string LogDirUI { get; } = Path.Combine(LogDir, "UI");
            
            /// <summary>
            /// 临时缓存（预览、临时更换）
            /// </summary>
            public static string TempDir { get; } = Path.Combine(AppDataDir, "temp");

            /// <summary>
            /// 壁纸存储目录
            /// </summary>
            public static string WpStoreDir { get; } = Path.Combine(AppDataDir, "Library");

            public static string AppRulesPath { get; } = Path.Combine(AppDataDir, "AppRules.json");
            public static string WallpaperLayoutPath { get; } = Path.Combine(AppDataDir, "WallpaperLayout.json");
            public static string UserSettingsPath { get; } = Path.Combine(AppDataDir, "UserSettings.json");

            public static string TempWebView2Dir { get; } = Path.Combine(AppDataDir, "WebView2");
        }

        public static class CommonPartialPaths
        {
            /// <summary>
            /// 壁纸文件存储目录（本地导入 + 云端下载） + 壁纸自定义配置文件存储目录
            /// </summary>
            public static string WallpaperInstallDir { get; } = "wallpapers";

            //public static string ActionsJsonDir { get; } = "actions";

            ///// <summary>
            ///// 壁纸自定义配置文件存储目录
            ///// </summary>
            //public static string WallpaperConfigDir { get; } = "wpconfigs";
        }

        public static class SingleInstance
        {
            public static string UniqueAppUid { get; } = "Virtual:WALLPAPERSYSTEM";
            public static string PipeServerName { get; } = UniqueAppUid + Environment.UserName;
            public static string GrpcPipeServerName { get; } = "Grpc_" + PipeServerName;
        }

        public static class ApplicationType
        {
            public static bool IsMSIX { get; } = new DesktopBridge.Helpers().IsRunningAsUwp();
            //todo: make compile-time flag.
            public static bool IsTestBuild { get; } = false;
        }

        public static class Weather
        {
            //todo: make compile-time flag.
            public static string OpenWeatherMapAPIKey = string.Empty;
        }
    }
}
