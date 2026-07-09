using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Cores.AppUpdate.Specific {
    public interface IInstallerUpdateService : IUpdateService {
        Task ExecuteAsync();
    }

    public class InstallerUpdateResult {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? InstallerPath { get; set; }
    }

    public class InstallerUpdateService(IDownloadService downloadService) : UpdateServiceBase<InstallerUpdateService>(downloadService), IInstallerUpdateService {
        private string? _installerPath;

        public async Task<bool> DownloadUpdateAsync(ReleaseInfo info, IProgress<DownloadProgress> progress, CancellationToken token) {
            if (info.InstallerUri == null)
                return false;

            var cacheDir = Constants.CommonPaths.PendingInstallerUpdateDir;
            var installerFileName = Path.GetFileName(info.InstallerUri.LocalPath);

            var (success, filePath, _) = await DownloadFileAsync(info.InstallerUri, cacheDir, installerFileName, progress, token);

            _installerPath = filePath;
            return success;
        }

        public async Task<bool> VerifyUpdateAsync(ReleaseInfo info, CancellationToken token) {
            if (_installerPath == null || info.InstallerShaUri == null)
                return false;

            try {
                var expectedHash = await downloadService.DownloadShaTxtAsync(info.InstallerShaUri, token);
                var verified = await downloadService.VerifyFileIntegrityAsync(_installerPath, expectedHash, token);

                if (!verified) {
                    throw new InvalidDataException("SHA256 verification failed for installer");
                }

                await SaveUpdateFlagAsync(new InstallerUpdateFlag {
                    Status = UpdateStatus.Pending,
                    InstallerPath = _installerPath,
                    Sha256 = expectedHash
                }, token);

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<InstallerUpdateService>().Error("Installer verify failed", ex);
                FileUtil.RemoveDirectory(Constants.CommonPaths.PendingInstallerUpdateDir);
                return false;
            }
        }

        public async Task ExecuteAsync() {
            var flag = await LoadUpdateFlagAsync();
            if (flag == null || flag.Status != UpdateStatus.Pending) {
                ArcLog.GetLogger<InstallerUpdateService>().Warn("No pending installer update found");
                return;
            }

            var installerPath = flag.InstallerPath;
            if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath)) {
                ArcLog.GetLogger<InstallerUpdateService>().Warn($"Installer not found: {installerPath}");
                FileUtil.RemoveDirectory(Constants.CommonPaths.PendingInstallerUpdateDir);
                return;
            }

            try {
                // Update flag status to in-progress
                flag.Status = UpdateStatus.InProgress;
                await SaveUpdateFlagAsync(flag);

                // Start installer as detached process
                Process.Start(new ProcessStartInfo {
                    FileName = installerPath,
                    Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(installerPath)
                });

                ArcLog.GetLogger<InstallerUpdateService>().Info("Installer started, exiting main process");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<InstallerUpdateService>().Error("Failed to start installer", ex);
                FileUtil.RemoveDirectory(Constants.CommonPaths.PendingInstallerUpdateDir);
            }
        }

        private async Task<InstallerUpdateFlag?> LoadUpdateFlagAsync(CancellationToken token = default) {
            var flagPath = Constants.CommonPaths.InstallerUpdateFlagPath;
            if (!File.Exists(flagPath)) return null;

            var json = await File.ReadAllTextAsync(flagPath, token);
            return JsonSerializer.Deserialize(json, UpdateFlagContext.Default.InstallerUpdateFlag);
        }

        private async Task SaveUpdateFlagAsync(InstallerUpdateFlag flag, CancellationToken token = default) {
            var flagPath = Constants.CommonPaths.InstallerUpdateFlagPath;
            Directory.CreateDirectory(Path.GetDirectoryName(flagPath)!);
            var json = JsonSerializer.Serialize(flag, UpdateFlagContext.Default.InstallerUpdateFlag);
            await File.WriteAllTextAsync(flagPath, json, token);
        }
    }
}
