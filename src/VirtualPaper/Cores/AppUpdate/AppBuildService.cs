using System.IO;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Cores.AppUpdate.Models;
using VirtualPaper.Models.AppUpdate;

namespace VirtualPaper.Cores.AppUpdate {
    public interface IAppBuildService {
        AppBuildInfo BuildInfo { get; }
        string AppBuild { get; }
        string GetPluginBuild(string pluginName);
        void Refresh();
    }

    public class AppBuildService : IAppBuildService {
        private const string AppCompManifestFile = "app_comp_manifest.json";

        public AppBuildInfo BuildInfo { get; private set; } = new();

        public string AppBuild => BuildInfo.AppBuildNumber;

        public AppBuildService() {
            Refresh();
        }

        public string GetPluginBuild(string pluginName) {
            return BuildInfo.Plugins.TryGetValue(pluginName, out var build) ? build : string.Empty;
        }

        public void Refresh() {
            SyncManifestToAppData();
            BuildInfo = LoadFromManifest() ?? new AppBuildInfo();
        }

        /// <summary>
        /// 将工作目录的 app_comp_manifest.json 同步到 AppData 目录。
        /// 若工作目录不存在该文件，则删除 AppData 目录下的副本。
        /// </summary>
        private static void SyncManifestToAppData() {
            var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppCompManifestFile);
            var destPath = Path.Combine(Constants.CommonPaths.AppDataDir, AppCompManifestFile);
            try {
                if (!File.Exists(sourcePath)) {
                    if (File.Exists(destPath)) File.Delete(destPath);
                    return;
                }
                Directory.CreateDirectory(Constants.CommonPaths.AppDataDir);
                File.Copy(sourcePath, destPath, true);
            }
            catch { }
        }

        private static AppBuildInfo? LoadFromManifest() {
            // 优先从 AppDataDir 读取，其次从 BaseDirectory
            var paths = new[] {
                Path.Combine(Constants.CommonPaths.AppDataDir, AppCompManifestFile),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppCompManifestFile)
            };

            foreach (var path in paths) {
                if (!File.Exists(path)) continue;
                try {
                    var json = File.ReadAllText(path);
                    var manifest = JsonSerializer.Deserialize(json, UpdateManifestContext.Default.AppCompManifest);
                    if (manifest?.Plugins == null) continue;

                    var info = new AppBuildInfo { AppBuildNumber = manifest.AppBuildNumber };
                    foreach (var (pluginName, buildNumber) in manifest.Plugins) {
                        info.Plugins[pluginName] = buildNumber;
                    }
                    return info;
                }
                catch {
                    continue;
                }
            }
            return null;
        }
    }
}
