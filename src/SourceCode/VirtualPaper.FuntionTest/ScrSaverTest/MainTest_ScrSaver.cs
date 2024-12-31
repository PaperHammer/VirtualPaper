using System.Diagnostics;
using System.IO;
using System.Text;

namespace VirtualPaper.FuntionTest.ScrSaverTest {
    internal class MainTest_ScrSaver {
        public static Process InitScr(string filePath, string type, string effect) {
            StringBuilder cmdArgs = new();
            cmdArgs.Append($" --file-path {filePath}");
            cmdArgs.Append($" --wallpaper-type {type}");
            cmdArgs.Append($" --effect {effect}");

            ProcessStartInfo start = new() {
                FileName = _fileName,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = _workingDir,
                Arguments = cmdArgs.ToString(),
            };

            Process _process = new() {
                EnableRaisingEvents = true,
                StartInfo = start,
            };

            return _process;
        }

        private readonly static string _workingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "ScrSaver");
        private readonly static string _fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "ScrSaver", "VirtualPaper.ScreenSaver.exe");
    }
}
