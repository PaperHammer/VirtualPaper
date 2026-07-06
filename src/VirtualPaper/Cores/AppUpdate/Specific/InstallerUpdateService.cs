using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Cores.AppUpdate.Specific {
    public interface IInstallerUpdateService {
        Task<InstallerUpdateResult> DownloadAsync(ReleaseInfo releaseInfo, IProgress<DownloadProgress>? progress = null, CancellationToken token = default);
        Task<InstallerUpdateResult> VerifyAsync(ReleaseInfo releaseInfo, string installerPath, CancellationToken token = default);
        Task ExecuteAsync();
    }

    public class InstallerUpdateResult {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? InstallerPath { get; set; }
    }

    public class InstallerUpdateService(IDownloadService downloadService) : IInstallerUpdateService {
        public async Task<InstallerUpdateResult> DownloadAsync(ReleaseInfo releaseInfo, IProgress<DownloadProgress>? progress = null, CancellationToken token = default) {
            var result = new InstallerUpdateResult();

            if (releaseInfo.InstallerUri == null) {
                result.Success = false;
                result.ErrorMessage = "No installer URI available";
                return result;
            }

            var cacheDir = Constants.CommonPaths.PendingInstallerUpdateDir;

            try {
                Directory.CreateDirectory(cacheDir);

                var installerFileName = Path.GetFileName(releaseInfo.InstallerUri.LocalPath);
                var installerPath = Path.Combine(cacheDir, installerFileName);

                await foreach (var p in downloadService.DownloadAsync(releaseInfo.InstallerUri, installerPath, token)) {
                    progress?.Report(p);
                }

                result.Success = true;
                result.InstallerPath = installerPath;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<InstallerUpdateService>().Error("Installer download failed", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                FileUtil.RemoveDirectory(cacheDir);
            }

            return result;
        }

        public async Task<InstallerUpdateResult> VerifyAsync(ReleaseInfo releaseInfo, string installerPath, CancellationToken token = default) {
            var result = new InstallerUpdateResult();

            if (releaseInfo.InstallerShaUri == null) {
                result.Success = false;
                result.ErrorMessage = "No installer SHA256 URI available";
                return result;
            }

            try {
                var expectedHash = await downloadService.DownloadShaTxtAsync(releaseInfo.InstallerShaUri, token);
                var verified = await downloadService.VerifyFileIntegrityAsync(installerPath, expectedHash, token);

                if (!verified) {
                    throw new InvalidDataException("SHA256 verification failed for installer");
                }

                // Write update flag after successful verification
                await SaveUpdateFlagAsync(new InstallerUpdateFlag {
                    Status = UpdateStatus.Pending,
                    InstallerPath = installerPath
                }, token);

                result.Success = true;
                result.InstallerPath = installerPath;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<InstallerUpdateService>().Error("Installer verify failed", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                FileUtil.RemoveDirectory(Constants.CommonPaths.PendingInstallerUpdateDir);
            }

            return result;
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
