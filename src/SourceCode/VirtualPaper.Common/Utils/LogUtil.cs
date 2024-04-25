using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VirtualPaper.Common.Utils.Archive;
using VirtualPaper.Common.Utils.Hardware;

namespace VirtualPaper.Common.Utils
{
    public static class LogUtil
    {
        /// <summary>
        /// Get hardware information
        /// </summary>
        public static string GetHardwareInfo()
        {
            var arch = Environment.Is64BitProcess ? "x86" : "x64";
            var container = Constants.ApplicationType.IsMSIX ? "desktop-bridge" : "desktop-native";
            return $"\nVirtual Paper v{Assembly.GetEntryAssembly().GetName().Version} {arch} {container} {CultureInfo.CurrentUICulture.Name}" +
                $"\n{SystemInfo.GetOSInfo()}\n{SystemInfo.GetCpuInfo()}\n{SystemInfo.GetGpuInfo()}\n";
        }

        /// <summary>
        /// Return string representation of win32 Error.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="memberName"></param>
        /// <param name="fileName"></param>
        /// <param name="lineNumber"></param>
        /// <returns></returns>
        public static string GetWin32Error(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string fileName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            int err = Marshal.GetLastWin32Error();
            return $"HRESULT: {err}, {message} at\n{fileName} ({lineNumber})\n{memberName}";
        }

        /// <summary>
        /// Let user create archive file with all the relevant diagnostic files.
        /// </summary>
        public static void ExportLogFiles(string savePath)
        {
            if (string.IsNullOrEmpty(savePath))
            {
                throw new ArgumentNullException(savePath);
            }

            var files = new List<string>();
            var logFolder = Constants.CommonPaths.LogDir;
            if (Directory.Exists(logFolder))
            {
                files.AddRange(Directory.GetFiles(logFolder, "*.*", SearchOption.TopDirectoryOnly));
            }

            var logFolderCore = Constants.CommonPaths.LogDirCore;
            if (Directory.Exists(logFolder))
            {
                files.AddRange(Directory.GetFiles(logFolderCore, "*.*", SearchOption.TopDirectoryOnly));
            }
            
            var logFolderUI = Constants.CommonPaths.LogDirUI;
            if (Directory.Exists(logFolder))
            {
                files.AddRange(Directory.GetFiles(logFolderUI, "*.*", SearchOption.TopDirectoryOnly));
            }

            var settingsFile = Constants.CommonPaths.UserSettingsPath;
            if (File.Exists(settingsFile))
            {
                files.Add(settingsFile);
            }

            var layoutFile = Constants.CommonPaths.WallpaperLayoutPath;
            if (File.Exists(layoutFile))
            {
                files.Add(layoutFile);
            }

            if (files.Count != 0)
            {
                ZipCreate.CreateZip(savePath,
                    new List<ZipCreate.FileData>() {
                                new() { ParentDirectory = Constants.CommonPaths.AppDataDir, Files = files } });
            }
        }
    }
}
