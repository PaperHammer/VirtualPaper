using System.Diagnostics;
using System.IO;
using NLog;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Services {
    public class WatchdogService : IWatchdogService {
        public bool IsRunning { get; private set; }

        public void Start() {
            if (_subProcess != null)
                return;

            _logger.Info("Starting watchdog service...");
            ProcessStartInfo start = new() {
                Arguments = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Watchdog", "VirtualPaper.Watchdog"),
                RedirectStandardInput = true,
                UseShellExecute = false,
            };

            _subProcess = new Process {
                StartInfo = start,
            };

            try {
                _subProcess.Start();
                IsRunning = true;
            }
            catch (Exception e) {
                _logger.Error("Failed to start watchdog service: " + e.Message);
            }
        }

        public void Add(int pid) {
            _logger.Info("Adding program to watchdog: " + pid);
            SendMessage("ADD " + pid);
        }

        public void Remove(int pid) {
            _logger.Info("Removing program to watchdog: " + pid);
            SendMessage("RMV " + pid);
        }

        public void Clear() {
            _logger.Info("Cleared watchdog program(s)..");
            SendMessage("CLR");
        }

        private void SendMessage(string text) {
            try {
                _subProcess?.StandardInput.WriteLine(text);
            }
            catch (Exception e) {
                _logger.Error("Failed to communicate with watchdog service: " + e.Message);
            }
        }

        private Process? _subProcess;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
