using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.Cores;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace Workloads.Creation.WebBackdrop.Models.SerializableData {
    public class WebDesignFileUtil {
        public string ProjectFolder { get; private set; }
        public string ProjectFilePath { get; private set; }
        public string ProjectName => Path.GetFileNameWithoutExtension(ProjectFilePath);
        public string EntryFilePath => Path.Combine(ProjectFolder, GetManifestFilePathByRole("entry") ?? _projectData?.File ?? "index.html");
        public bool IsSaveFromInit { get; internal set; }

        private WpWebProjectData? _projectData;

        private WebDesignFileUtil(string identify) {
            var fullPath = Path.GetFullPath(identify);
            // 支持两种输入：.vpw 项目文件，或项目目录。
            // .vpw：直接作为项目入口文件，所在目录作为项目目录。
            // 目录：作为项目目录，并约定目录下的 {目录名}.vpw 为项目入口文件。
            var isProjectFile = Path.GetExtension(fullPath).Equals(FileExtension.FE_WebDesign, StringComparison.OrdinalIgnoreCase);

            ProjectFilePath = isProjectFile
                ? fullPath
                : Path.Combine(fullPath, Path.GetFileName(fullPath) + FileExtension.FE_WebDesign);
            ProjectFolder = isProjectFile
                ? Path.GetDirectoryName(ProjectFilePath)!
                : fullPath;

            IsSaveFromInit = File.Exists(ProjectFilePath);
            _projectData = LoadProjectData();
        }

        public static WebDesignFileUtil Create(string identify) {
            if (string.IsNullOrWhiteSpace(identify)) throw new ArgumentException("Input cannot be empty");

            // identify 可能是 .vpw 文件、绝对项目目录，或只包含项目名。
            // .vpw/绝对路径保持原样；项目名放到默认文档目录下创建。
            var path = Path.GetExtension(identify).Equals(FileExtension.FE_WebDesign, StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(identify)
                ? identify
                : Path.Combine(FileUtil.GetDocumentsDir(), identify);
            return new WebDesignFileUtil(path);
        }

        public WpWebProjectData GetOrCreateProjectData() {
            _projectData ??= new WpWebProjectData { Title = ProjectName };
            return _projectData;
        }

        public void SaveProjectData(WpWebProjectData data) {
            _projectData = data;
            var json = JsonSerializer.Serialize(data, WpWebProjectDataContext.Default.WpWebProjectData);
            WriteAllTextAtomic(GetProjectDataFilePath(), json);
        }

        public void EnsureProjectStructure() {
            Directory.CreateDirectory(ProjectFolder);
            CopyTemplateIfNeeded();
            EnsureProjectFile();
            RenameProjectFileInManifest();
            _projectData ??= LoadProjectData();

            if (!File.Exists(GetProjectDataFilePath())) {
                SaveProjectData(GetOrCreateProjectData());
                AddManifestPath(GetProjectDataFilePath(), "metadata");
            }
        }

        public IReadOnlyList<WebProjectManifestItem> GetManifestItems() {
            // 导出等在后台线程执行，与 UI 线程的 manifest 写入并发；在锁内完成快照，避免竞态
            lock (_manifestLock) {
                return GetManifestFiles()
                    .Select(item => new WebProjectManifestItem(
                        item["path"]?.GetValue<string>() ?? string.Empty,
                        item["type"]?.GetValue<string>() ?? "file",
                        item["role"]?.GetValue<string>() ?? "asset"))
                    .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                    .ToList();
            }
        }

        public void AddManifestPath(string path, string? role = null) {
            AddManifestPaths([path], role);
        }

        /// <summary>
        /// 批量登记路径：一次加载、一次保存 manifest，避免逐文件读写造成 O(N²) 磁盘 IO。
        /// </summary>
        public void AddManifestPaths(IEnumerable<string> paths, string? role = null) {
            lock (_manifestLock) {
                var manifest = LoadOrCreateManifest();
                var files = GetOrCreateFilesArray(manifest);
                var changed = false;

                foreach (var path in paths) {
                    if (IsProjectFile(path) && role == null) continue;

                    var relativePath = ToRelativePath(path);
                    if (files.OfType<JsonObject>().Any(item => IsSameManifestPath(item["path"]?.GetValue<string>(), relativePath))) continue;

                    files.Add(new JsonObject {
                        ["path"] = relativePath,
                        ["type"] = Directory.Exists(path) ? "folder" : GetFileType(relativePath),
                        ["role"] = role ?? GetFileRole(relativePath)
                    });
                    changed = true;
                }

                if (changed) SaveManifest(manifest);
            }
        }

        public void AddManifestPathRecursive(string path) {
            if (!Directory.Exists(path)) {
                AddManifestPath(path);
                return;
            }

            // 一次批量登记：目录自身 + 全部子目录与文件，只读写一次 manifest
            AddManifestPaths(new[] { path }
                .Concat(Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)));
        }

        public void RemoveManifestPath(string path) {
            if (IsProjectFile(path)) return;

            lock (_manifestLock) {
                var relativePath = ToRelativePath(path);
                var manifest = LoadOrCreateManifest();
                if (manifest["files"] is not JsonArray files) return;

                for (var i = files.Count - 1; i >= 0; i--) {
                    if (files[i] is not JsonObject item) continue;
                    var itemPath = item["path"]?.GetValue<string>();
                    if (IsSameManifestPath(itemPath, relativePath) || IsManifestChildPath(itemPath, relativePath)) {
                        files.RemoveAt(i);
                    }
                }
                SaveManifest(manifest);
            }
        }

        public void RenameManifestPath(string oldPath, string newPath) {
            if (IsProjectFile(oldPath)) return;

            lock (_manifestLock) {
                var oldRelativePath = ToRelativePath(oldPath);
                var newRelativePath = ToRelativePath(newPath);
                var manifest = LoadOrCreateManifest();
                if (manifest["files"] is not JsonArray files) return;

                foreach (var item in files.OfType<JsonObject>()) {
                    var itemPath = item["path"]?.GetValue<string>();
                    if (IsSameManifestPath(itemPath, oldRelativePath)) {
                        item["path"] = newRelativePath;
                    }
                    else if (IsManifestChildPath(itemPath, oldRelativePath)) {
                        item["path"] = newRelativePath + itemPath![oldRelativePath.Length..];
                    }
                }
                SaveManifest(manifest);
            }
        }

        private void CopyTemplateIfNeeded() {
            if (Directory.EnumerateFileSystemEntries(ProjectFolder).Any()) return;

            var templatePath = Path.Combine(AppContext.BaseDirectory, Constants.ModuleName.WebBackdrop, "Assets", "templates", "v1");
            if (!Directory.Exists(templatePath)) return;

            CopyDirectory(templatePath, ProjectFolder);
        }

        private static void CopyDirectory(string source, string target) {
            Directory.CreateDirectory(target);

            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) {
                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
            }

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
                File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), false);
            }
        }

        private void EnsureProjectFile() {
            if (File.Exists(ProjectFilePath)) return;

            var templateProjectFile = Path.Combine(ProjectFolder, "template" + FileExtension.FE_WebDesign);
            if (File.Exists(templateProjectFile) && !templateProjectFile.Equals(ProjectFilePath, StringComparison.OrdinalIgnoreCase)) {
                File.Move(templateProjectFile, ProjectFilePath);
                _manifestCache = null; // 工程文件刚就位，丢弃旧缓存重新加载
                IsSaveFromInit = true;
                return;
            }

            File.WriteAllText(ProjectFilePath, CreateProjectManifest());
            _manifestCache = null; // 新生成的清单与可能已缓存的空清单不一致，丢弃缓存
            IsSaveFromInit = true;
        }

        private void RenameProjectFileInManifest() {
            if (!File.Exists(ProjectFilePath)) return;

            lock (_manifestLock) {
                var manifest = LoadOrCreateManifest();
                manifest["name"] = ProjectName;
                manifest.Remove("project");
                manifest.Remove("entry");

                if (manifest["files"] is JsonArray files) {
                    foreach (var item in files.OfType<JsonObject>()) {
                        if (item["role"]?.GetValue<string>() == "solution") {
                            item["path"] = Path.GetFileName(ProjectFilePath);
                        }
                    }
                }

                SaveManifest(manifest);
            }
        }

        private string CreateProjectManifest() {
            var files = Directory.EnumerateDirectories(ProjectFolder, "*", SearchOption.AllDirectories)
                .Select(path => CreateManifestFileItem(path))
                .Concat(Directory.EnumerateFiles(ProjectFolder, "*", SearchOption.AllDirectories)
                    .Select(path => CreateManifestFileItem(path)));

            var manifest = new JsonObject {
                ["version"] = 1,
                ["name"] = ProjectName,
                ["files"] = new JsonArray(files.Select(file => JsonNode.Parse(file.ToJsonString())!).ToArray())
            };

            if (manifest["files"]!.AsArray().OfType<JsonObject>().All(item => item["role"]?.GetValue<string>() != "solution")) {
                manifest["files"]!.AsArray().Add(new JsonObject {
                    ["path"] = Path.GetFileName(ProjectFilePath),
                    ["type"] = "vpw",
                    ["role"] = "solution"
                });
            }

            return manifest.ToJsonString(_jsonSerializerOptions);
        }

        private JsonObject CreateManifestFileItem(string path) {
            var relativePath = ToRelativePath(path);
            return new JsonObject {
                ["path"] = relativePath,
                ["type"] = Directory.Exists(path) ? "folder" : GetFileType(relativePath),
                ["role"] = IsProjectFile(path) ? "solution" : GetFileRole(relativePath)
            };
        }

        private string GetProjectDataFilePath() {
            return Path.Combine(ProjectFolder, GetManifestFilePathByRole("metadata") ?? "project.json");
        }

        private string? GetManifestFilePathByRole(string role) {
            return GetManifestFiles()
                .OfType<JsonObject>()
                .FirstOrDefault(item => item["role"]?.GetValue<string>() == role)?["path"]?.GetValue<string>();
        }

        private IEnumerable<JsonObject> GetManifestFiles() {
            // 加锁保护：manifest 缓存会被后台线程（导出）与 UI 线程并发访问
            lock (_manifestLock) {
                var manifest = LoadOrCreateManifest();
                return manifest["files"] is JsonArray files
                    ? files.OfType<JsonObject>().ToList()
                    : [];
            }
        }

        private JsonObject LoadOrCreateManifest() {
            if (_manifestCache != null) return _manifestCache;

            if (File.Exists(ProjectFilePath)) {
                try {
                    if (JsonNode.Parse(File.ReadAllText(ProjectFilePath)) is JsonObject node) {
                        _parseFailed = false;
                        _manifestCache = node;
                        return node;
                    }
                }
                catch (Exception ex) {
                    _parseFailed = true;
                    GlobalMessageUtil.ShowError(
                        $"Failed to parse project file: {ProjectFilePath}\n{ex.Message}",
                        key: nameof(ProjectFilePath));
                }
            }

            _manifestCache = [];
            return _manifestCache;
        }

        /// <summary>
        /// 丢弃内存中的 manifest 缓存（如磁盘上的 .vpw 被外部修改后），下次访问时重新从磁盘加载。
        /// </summary>
        public void ReloadManifest() {
            lock (_manifestLock) {
                _manifestCache = null;
                _parseFailed = false;
            }
        }

        private static JsonArray GetOrCreateFilesArray(JsonObject manifest) {
            if (manifest["files"] is JsonArray files) return files;

            files = [];
            manifest["files"] = files;
            return files;
        }

        private void SaveManifest(JsonObject manifest) {
            // 解析失败时禁止保存，避免覆盖可能被恢复的数据
            if (_parseFailed) {
                GlobalMessageUtil.ShowError(
                    $"Cannot save project file: {ProjectFilePath}\n" +
                    "The file is corrupted. Please fix or restore it before making changes.",
                    key: "VpwSaveBlocked");
                return;
            }

            // 保存前自动去重：按 path 保留首次出现的条目
            if (manifest["files"] is JsonArray files) {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = files.Count - 1; i >= 0; i--) {
                    var path = files[i]?["path"]?.GetValue<string>();
                    if (path == null || !seen.Add(path)) {
                        files.RemoveAt(i);
                    }
                }
            }

            WriteAllTextAtomic(ProjectFilePath, manifest.ToJsonString(_jsonSerializerOptions));
            _manifestCache = manifest;
        }

        /// <summary>
        /// 原子写入：先写临时文件再替换目标，避免写入中途崩溃留下损坏的 manifest/project.json。
        /// 临时文件以 "." 开头、".tmp" 结尾，ProjectFileManager 会将其识别为瞬时文件而忽略。
        /// </summary>
        private static void WriteAllTextAtomic(string path, string content) {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Invalid file path.", nameof(path));

            var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try {
                File.WriteAllText(tempPath, content);
                File.Move(tempPath, path, overwrite: true);
            }
            finally {
                if (File.Exists(tempPath)) {
                    try { File.Delete(tempPath); } catch { /* 忽略清理失败 */ }
                }
            }
        }

        private string ToRelativePath(string path) {
            return Path.GetRelativePath(ProjectFolder, path).Replace(Path.DirectorySeparatorChar, '/');
        }

        public bool IsProjectFile(string path) {
            return string.Equals(Path.GetFullPath(path), ProjectFilePath, StringComparison.OrdinalIgnoreCase);
        }

        public void UpdateProjectFilePath(string newPath) {
            ProjectFilePath = newPath;
            ProjectFolder = Path.GetDirectoryName(newPath)!;
            _manifestCache = null; // 项目文件路径变更，缓存失效
        }

        private static bool IsSameManifestPath(string? left, string right) {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsManifestChildPath(string? path, string parentPath) {
            return path != null && path.StartsWith(parentPath + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFileType(string fileName) => WebEditorFileUtil.GetManifestFileTypeFromExtension(Path.GetExtension(fileName));

        private static string GetFileRole(string fileName) => fileName.ToLowerInvariant() switch {
            "index.html" => "entry",
            "style.css" => "style",
            "script.js" => "script",
            "project.json" => "metadata",
            _ when Path.GetExtension(fileName).Equals(FileExtension.FE_WebDesign, StringComparison.OrdinalIgnoreCase) => "solution",
            _ => "asset"
        };

        private WpWebProjectData? LoadProjectData() {
            var projectDataPath = GetProjectDataFilePath();
            if (!File.Exists(projectDataPath)) return null;
            try {
                var json = File.ReadAllText(projectDataPath);
                var data = JsonSerializer.Deserialize(json, WpWebProjectDataContext.Default.WpWebProjectData);
                return data == null ? null : ResolveProjectI18n(data, json);
            }
            catch {
                return null;
            }
        }

        private static WpWebProjectData ResolveProjectI18n(WpWebProjectData data, string json) {
            var root = JsonNode.Parse(json) as JsonObject;
            if (root?["i18n"] is not JsonObject i18n) return data;

            var cultureName = CultureInfo.CurrentUICulture.Name;
            var languageName = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var values = i18n[cultureName] as JsonObject
                ?? i18n[languageName] as JsonObject
                ?? i18n["en-US"] as JsonObject;
            if (values == null) return data;

            data.Title = ResolveI18nValue(data.Title, values);
            data.Desc = ResolveI18nValue(data.Desc, values);
            data.Authors = ResolveI18nValue(data.Authors, values);
            data.Tags = ResolveI18nValue(data.Tags, values);
            return data;
        }

        private static string ResolveI18nValue(string value, JsonObject values) {
            const string prefix = "{{i18n:";
            const string suffix = "}}";

            if (!value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith(suffix, StringComparison.Ordinal)) return value;

            var key = value[prefix.Length..^suffix.Length];
            return values[key]?.GetValue<string>() ?? value;
        }

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
        private readonly object _manifestLock = new();
        private JsonObject? _manifestCache;
        private bool _parseFailed;
    }

    public record WebProjectManifestItem(string Path, string Type, string Role);
}
