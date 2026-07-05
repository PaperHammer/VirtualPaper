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
        private const string AppManifestFile = "app_manifest.json";

        public AppBuildInfo BuildInfo { get; private set; } = new();

        public string AppBuild => BuildInfo.AppBuild;

        public AppBuildService() {
            Refresh();
        }

        public string GetPluginBuild(string pluginName) {
            return BuildInfo.Plugins.TryGetValue(pluginName, out var build) ? build : string.Empty;
        }

        public void Refresh() {
            BuildInfo = LoadFromManifest() ?? new AppBuildInfo();
        }

        private static AppBuildInfo? LoadFromManifest() {
            // 优先从 AppDataDir 读取，其次从 BaseDirectory
            var paths = new[] {
                Path.Combine(Constants.CommonPaths.AppDataDir, AppManifestFile),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppManifestFile)
            };

            foreach (var path in paths) {
                if (!File.Exists(path)) continue;
                try {
                    var json = File.ReadAllText(path);
                    var manifest = JsonSerializer.Deserialize(json, UpdateManifestContext.Default.UpdateManifest);
                    if (manifest?.AppPluginsInfo == null) continue;

                    var info = new AppBuildInfo { AppBuild = manifest.AppBuild };
                    foreach (var (pluginName, pluginInfo) in manifest.AppPluginsInfo) {
                        info.Plugins[pluginName] = pluginInfo.BuildNumber;
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
