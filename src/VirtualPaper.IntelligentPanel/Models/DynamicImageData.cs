using System;
using System.Linq;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.ML.DynamicImage.Models;

namespace VirtualPaper.IntelligentPanel.Models {
    public sealed class DynamicImageData : IIntelliData {
        public Guid Id { get; } = Guid.NewGuid();
        public string SourceFilePath { get; }
        public string SourceFileSize { get; }
        public string SourceFileExt { get; }
        public uint Width { get; }
        public uint Height { get; }
        public DynamicImageQuality Quality { get; }

        public string? ResultDirectory { get; private set; }
        public string? PreviewFilePath { get; private set; }
        public string? WebPackagePath { get; private set; }
        public string? ManifestFilePath { get; private set; }
        public string? ResultSize { get; private set; }
        public int LayerCount { get; private set; }
        public int ObjectCount { get; private set; }
        public TimeSpan ProcessingTime { get; private set; }

        public DynamicImageData(
            string sourceFilePath,
            string sourceFileSize,
            string sourceFileExt,
            uint width,
            uint height,
            DynamicImageQuality quality) {

            SourceFilePath = sourceFilePath;
            SourceFileSize = sourceFileSize;
            SourceFileExt = sourceFileExt;
            Width = width;
            Height = height;
            Quality = quality;
        }

        public void SetResult(
            DynamicImageExportResult export,
            DynamicImageAnalysisResult analysis) {

            ResultDirectory = export.OutputDirectory;
            WebPackagePath = export.WebPackagePath;
            PreviewFilePath = !string.IsNullOrWhiteSpace(export.WebPackagePath) &&
                System.IO.File.Exists(export.WebPackagePath)
                ? export.WebPackagePath
                : export.LayerOrderPreviewPath;
            ManifestFilePath = export.ManifestPath;
            ResultSize = FileUtil.SizeSuffix(FileUtil.GetDirectorySize(export.OutputDirectory));
            LayerCount = analysis.LayerPlan.Layers.Count;
            ObjectCount = analysis.LayerPlan.Layers.Count(layer => layer.IsObject);
            ProcessingTime = analysis.Timing.Total;
        }
    }
}
