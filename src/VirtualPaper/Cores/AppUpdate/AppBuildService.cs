using System.IO;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Models.AppUpdate;

namespace VirtualPaper.Cores.AppUpdate {
    public interface IAppBuildService {
        AppBuildInfo BuildInfo { get; }
        string AppBuild { get; }
        string GetPluginBuild(string pluginName);
        void Refresh();
        Task SaveAsync();
    }

    public class AppBuildService : IAppBuildService {
        public AppBuildInfo BuildInfo { get; private set; } = new();

        public string AppBuild => BuildInfo.AppBuild;

        public AppBuildService() {
            Refresh();
        }

        public string GetPluginBuild(string pluginName) {
            return BuildInfo.Plugins.TryGetValue(pluginName, out var build) ? build : string.Empty;
        }

        public void Refresh() {
            var path = GetFilePath();
            if (File.Exists(path)) {
                BuildInfo = LoadFromFile(path);
            }
            else {
                BuildInfo = new AppBuildInfo();
            }
        }

        private static string GetFilePath() {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.CoreField.AppBuildFile);
        }

        private static AppBuildInfo LoadFromFile(string path) {
            try {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize(json, AppBuildInfoContext.Default.AppBuildInfo) ?? new AppBuildInfo();
            }
            catch {
                return new AppBuildInfo();
            }
        }

        public async Task SaveAsync() {
            var path = GetFilePath();
            var json = JsonSerializer.Serialize(BuildInfo, AppBuildInfoContext.Default.AppBuildInfo);
            await File.WriteAllTextAsync(path, json);
        }
    }
}
