using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.Cores;

namespace Workloads.Creation.WebBackdrop.Models.SerializableData {
    public class WebDesignFileUtil {
        public string ProjectFolder { get; private set; }
        public string ProjectFilePath { get; private set; }
        public string ProjectName => Path.GetFileNameWithoutExtension(ProjectFilePath);
        public string EntryFilePath => Path.Combine(ProjectFolder, _projectData?.File ?? "index.html");
        public bool IsSaveFromInit { get; internal set; }

        private WpWebProjectData? _projectData;

        private WebDesignFileUtil(string projectFilePath) {
            if (Path.GetExtension(projectFilePath).Equals(FileExtension.FE_WebDesign, StringComparison.OrdinalIgnoreCase)) {
                ProjectFilePath = Path.GetFullPath(projectFilePath);
                ProjectFolder = Path.GetDirectoryName(ProjectFilePath)!;
            }
            else {
                ProjectFolder = Path.GetFullPath(projectFilePath);
                ProjectFilePath = Path.Combine(ProjectFolder, Path.GetFileName(ProjectFolder) + FileExtension.FE_WebDesign);
            }

            IsSaveFromInit = File.Exists(ProjectFilePath);
            _projectData = LoadProjectData();
        }

        public static WebDesignFileUtil Create(string identify) {
            if (string.IsNullOrWhiteSpace(identify)) throw new ArgumentException("Input cannot be empty");

            if (Path.GetExtension(identify).Equals(FileExtension.FE_WebDesign, StringComparison.OrdinalIgnoreCase)) {
                return new WebDesignFileUtil(identify);
            }

            var dir = Path.IsPathRooted(identify)
                ? identify
                : Path.Combine(FileUtil.GetDocumentsDir(), identify);
            return new WebDesignFileUtil(dir);
        }

        public WpWebProjectData GetOrCreateProjectData() {
            _projectData ??= new WpWebProjectData { Title = ProjectName };
            return _projectData;
        }

        public void SaveProjectData(WpWebProjectData data) {
            _projectData = data;
            var json = JsonSerializer.Serialize(data, WpWebProjectDataContext.Default.WpWebProjectData);
            File.WriteAllText(Path.Combine(ProjectFolder, "project.json"), json);
        }

        public void EnsureProjectStructure() {
            Directory.CreateDirectory(ProjectFolder);
            CopyTemplateIfNeeded();
            EnsureProjectFile();
            RenameProjectFileInManifest();

            var projectJson = Path.Combine(ProjectFolder, "project.json");
            if (!File.Exists(projectJson)) {
                SaveProjectData(GetOrCreateProjectData());
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
                IsSaveFromInit = true;
                return;
            }

            File.WriteAllText(ProjectFilePath, CreateProjectManifest());
            IsSaveFromInit = true;
        }

        private void RenameProjectFileInManifest() {
            if (!File.Exists(ProjectFilePath)) return;

            try {
                var node = JsonNode.Parse(File.ReadAllText(ProjectFilePath)) as JsonObject;
                if (node == null) return;

                node["name"] = ProjectName;
                if (node["files"] is JsonArray files) {
                    foreach (var item in files.OfType<JsonObject>()) {
                        if (item["role"]?.GetValue<string>() == "solution") {
                            item["path"] = Path.GetFileName(ProjectFilePath);
                        }
                    }
                }

                File.WriteAllText(ProjectFilePath, node.ToJsonString(_jsonSerializerOptions));
            }
            catch { }
        }

        private string CreateProjectManifest() {
            var files = Directory.EnumerateFiles(ProjectFolder, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new JsonObject {
                    ["path"] = name,
                    ["type"] = GetFileType(name!),
                    ["role"] = GetFileRole(name!)
                });

            var manifest = new JsonObject {
                ["version"] = 1,
                ["name"] = ProjectName,
                ["files"] = new JsonArray(files.Select(file => JsonNode.Parse(file.ToJsonString())!).ToArray())
            };

            manifest["files"]!.AsArray().Add(new JsonObject {
                ["path"] = Path.GetFileName(ProjectFilePath),
                ["type"] = "vpw",
                ["role"] = "solution"
            });

            return manifest.ToJsonString(_jsonSerializerOptions);
        }

        private static string GetFileType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch {
            ".html" => "html",
            ".css" => "css",
            ".js" => "javascript",
            ".json" => "json",
            ".vpw" => "vpw",
            _ => "file"
        };

        private static string GetFileRole(string fileName) => fileName.ToLowerInvariant() switch {
            "index.html" => "entry",
            "style.css" => "style",
            "script.js" => "script",
            "project.json" => "metadata",
            _ when Path.GetExtension(fileName).Equals(FileExtension.FE_WebDesign, StringComparison.OrdinalIgnoreCase) => "solution",
            _ => "asset"
        };

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

        private WpWebProjectData? LoadProjectData() {
            var projectJson = Path.Combine(ProjectFolder, "project.json");
            if (!File.Exists(projectJson)) return null;
            try {
                var json = File.ReadAllText(projectJson);
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
    }
}
