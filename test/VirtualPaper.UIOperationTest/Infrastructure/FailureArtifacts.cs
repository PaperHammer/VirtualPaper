using VirtualPaper.Common;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class FailureArtifacts {
    public static void Save(string caseName, Dictionary<string, object?> evidence) {
        string relative = Path.Combine("logs", "UIOperation", "failed-" + caseName);
        string output = Path.Combine(Constants.CommonPaths.AppDataDir, relative);
        try {
            Directory.CreateDirectory(output);
            foreach (string source in new[] { Constants.CommonPaths.LogDirCore, Constants.CommonPaths.LogDirUI }) {
                if (!Directory.Exists(source)) continue;
                string target = Path.Combine(output, Path.GetFileName(source));
                Directory.CreateDirectory(target);
                foreach (string path in Directory.GetFiles(source, "*.txt"))
                    File.Copy(path, Path.Combine(target, Path.GetFileName(path)), overwrite: true);
            }
            foreach (string path in new[] { Constants.CommonPaths.UserSettingsPath, Constants.CommonPaths.WallpaperLayoutPath })
                if (File.Exists(path)) File.Copy(path, Path.Combine(output, Path.GetFileName(path)), overwrite: true);
            evidence["FailureArtifacts"] = relative.Replace('\\', '/');
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            evidence["FailureArtifactCaptureError"] = ex.Message;
        }
    }
}
