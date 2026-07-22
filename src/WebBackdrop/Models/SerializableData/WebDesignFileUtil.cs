using System;
using System.IO;
using System.Text.Json;
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

            if (!File.Exists(EntryFilePath)) {
                File.WriteAllText(EntryFilePath, DefaultHtmlTemplate());
            }

            var cssPath = Path.Combine(ProjectFolder, "style.css");
            if (!File.Exists(cssPath)) {
                File.WriteAllText(cssPath, DefaultCssTemplate());
            }

            var jsPath = Path.Combine(ProjectFolder, "script.js");
            if (!File.Exists(jsPath)) {
                File.WriteAllText(jsPath, DefaultJsTemplate());
            }

            var assetsPath = Path.Combine(ProjectFolder, "assets");
            Directory.CreateDirectory(assetsPath);

            var tsPath = Path.Combine(ProjectFolder, "example.ts");
            if (!File.Exists(tsPath)) {
                File.WriteAllText(tsPath, "export const message: string = 'Hello TypeScript';\n");
            }

            var jsxPath = Path.Combine(ProjectFolder, "component.jsx");
            if (!File.Exists(jsxPath)) {
                File.WriteAllText(jsxPath, "export function Component() {\n    return <div>Hello JSX</div>;\n}\n");
            }

            var tsxPath = Path.Combine(ProjectFolder, "component.tsx");
            if (!File.Exists(tsxPath)) {
                File.WriteAllText(tsxPath, "type ComponentProps = { title: string };\n\nexport function Component({ title }: ComponentProps) {\n    return <div>{title}</div>;\n}\n");
            }

            var dataJsonPath = Path.Combine(ProjectFolder, "data.json");
            if (!File.Exists(dataJsonPath)) {
                File.WriteAllText(dataJsonPath, "{\n    \"name\": \"VirtualPaper\"\n}\n");
            }

            var svgPath = Path.Combine(ProjectFolder, "logo.svg");
            if (!File.Exists(svgPath)) {
                File.WriteAllText(svgPath, "<svg width=\"64\" height=\"64\" viewBox=\"0 0 64 64\" xmlns=\"http://www.w3.org/2000/svg\">\n    <circle cx=\"32\" cy=\"32\" r=\"24\" fill=\"#33A9DC\"/>\n</svg>\n");
            }

            var imagePath = Path.Combine(ProjectFolder, "image.png");
            if (!File.Exists(imagePath)) {
                File.WriteAllBytes(imagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/luzf7wAAAABJRU5ErkJggg=="));
            }

            var mdPath = Path.Combine(ProjectFolder, "notes.md");
            if (!File.Exists(mdPath)) {
                File.WriteAllText(mdPath, "# Notes\n");
            }

            var filePath = Path.Combine(ProjectFolder, "LICENSE");
            if (!File.Exists(filePath)) {
                File.WriteAllText(filePath, "Mock file\n");
            }

            var projectFilePath = ProjectFilePath;
            if (!File.Exists(projectFilePath)) {
                File.WriteAllText(projectFilePath, string.Empty);
            }

            var projectJson = Path.Combine(ProjectFolder, "project.json");
            if (!File.Exists(projectJson)) {
                SaveProjectData(GetOrCreateProjectData());
            }
        }

        private WpWebProjectData? LoadProjectData() {
            var projectJson = Path.Combine(ProjectFolder, "project.json");
            if (!File.Exists(projectJson)) return null;
            try {
                var json = File.ReadAllText(projectJson);
                return JsonSerializer.Deserialize(json, WpWebProjectDataContext.Default.WpWebProjectData);
            }
            catch {
                return null;
            }
        }

        private static string DefaultHtmlTemplate() =>
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>Wallpaper</title>
                <link rel="stylesheet" href="style.css" />
            </head>
            <body>
                <canvas id="canvas"></canvas>
                <script src="script.js"></script>
            </body>
            </html>
            """;

        private static string DefaultCssTemplate() =>
            """
            * {
                margin: 0;
                padding: 0;
                box-sizing: border-box;
            }

            body {
                width: 100vw;
                height: 100vh;
                overflow: hidden;
                background: #000;
            }

            canvas {
                display: block;
                width: 100%;
                height: 100%;
            }
            """;

        private static string DefaultJsTemplate() =>
            """
            const canvas = document.getElementById('canvas');
            const ctx = canvas.getContext('2d');

            function resize() {
                canvas.width = window.innerWidth;
                canvas.height = window.innerHeight;
            }

            window.addEventListener('resize', resize);
            resize();

            function draw() {
                ctx.clearRect(0, 0, canvas.width, canvas.height);
                requestAnimationFrame(draw);
            }

            draw();
            """;
    }
}
