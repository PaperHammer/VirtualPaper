using System;
using System.IO;
using System.Text.Json;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.Cores;

namespace Workloads.Creation.WebBackdrop.Models.SerializableData {
    public class WebDesignFileUtil {
        public string ProjectFolder { get; private set; }
        public string ProjectName => Path.GetFileName(ProjectFolder);
        public string EntryFilePath => Path.Combine(ProjectFolder, _projectData?.File ?? "index.html");
        public bool IsSaveFromInit { get; internal set; }

        private WpWebProjectData? _projectData;

        private WebDesignFileUtil(string folderPath) {
            ProjectFolder = Path.GetFullPath(folderPath);
            IsSaveFromInit = Directory.Exists(folderPath);
            _projectData = LoadProjectData();
        }

        public static WebDesignFileUtil Create(string identify) {
            if (string.IsNullOrWhiteSpace(identify)) throw new ArgumentException("Input cannot be empty");

            if (FileUtil.IsValidFilePath(identify)) {
                // identify 可以是项目文件夹路径，或者 index.html 路径
                var folder = Directory.Exists(identify) ? identify : Path.GetDirectoryName(identify)!;
                return new WebDesignFileUtil(folder);
            }

            // 只是一个名字，在 Documents 下创建
            var dir = Path.Combine(FileUtil.GetDocumentsDir(), identify);
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
