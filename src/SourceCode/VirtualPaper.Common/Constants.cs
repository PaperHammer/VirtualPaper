using System.Net;

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
            public static string CommonDataDir { get; } = Path.Combine(AppDataDir, "data");

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

            public static string ExeIconDir { get; } = Path.Combine(CommonDataDir, "exeIcon");

            /// <summary>
            /// 壁纸存储目录
            /// </summary>
            public static string WpStoreDir { get; } = Path.Combine(AppDataDir, "Library");
            public static string ScrSaverDir { get; } = Path.Combine(AppDataDir, "ScrSaver");

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

        public static string ScrSaverHtml { get; } =
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title></title>
            </head>
            
            <body>
                <div class="content">
                </div>
            </body>
            </html>
            
            <script>
                let curVideoElementId = null;
            
                function virtualPaperSourceReload(wallpaperType, filePath) {
                    if (curVideoElementId) {
                        handleVideoCleanup();
                        curVideoElementId = null;
                    }
            
                    const element = document.querySelector('.content');
                    element.innerHTML = '';
            
                    if (wallpaperType && filePath) {
                        let elementToInsert;
                        switch (wallpaperType) {
                            case 'gif':
                            case 'picture':
                                elementToInsert = `<img class="full-screen" src="${filePath}"/>`;
                                break;
                            case 'video':
                                curVideoElementId = 'videoEle';
                                elementToInsert = `
                              <video id="videoEle" class="full-screen" loop>
                                  <source src="${filePath}">
                              </video>`;
                                break;
                            default:
                                return;
                        }
            
                        element.insertAdjacentHTML('beforeend', elementToInsert);
                    }
            
                    return "success";
                }
            
                function play() {
                    var videoElement = document.getElementById('videoEle');
                    videoElement.play();
                }
            
                function handleVideoCleanup() {
                    var videoElement = document.getElementById('videoEle');
                    videoElement.pause();
                    videoElement.removeAttribute('src');
                    videoElement = null;
                }
            </script>
            
            <style>
                body, html {
                    margin: 0;
                    padding: 0;
                    width: 100%;
                }
            
                .content {
                    height: 100vh;
                    overflow: hidden;
                }
            
                .full-screen {
                    position: relative;
                    width: 100%;
                    height: 100%;
                    object-fit: cover;
                }
            </style>
            """;
    }
}
