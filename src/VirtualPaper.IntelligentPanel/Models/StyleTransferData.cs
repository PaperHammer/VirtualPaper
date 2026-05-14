using System;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.IntelligentPanel.Models {
    public partial record StyleTransferData : IIntelliData {
        public Guid Id { get; }
        public string SourceFilePath { get; }
        public string SourceResolution => $"{Width} * {Height}";
        public string SourceFileSize { get; }
        public string SourceFileExt { get; }

        public string StyleFilePath { get; }
        public string? StyleName { get; }
        public string StyleFileSize { get; }
        public string StyleFileExt { get; }

        public string? ResultFilePath { get; private set; }
        public string? ResultResolution { get; private set; }
        public string? ResultFileSize { get; private set; }
        public string? ResultFileExt { get; private set; }

        public uint Width { get; }
        public uint Height { get; }

        public StyleTransferData(
            string sourcePath, string sourceFileSize, string sourceFileExt, uint width, uint height,
            string styleImagePath, string? styleName, string styleFileSize, string styleFileExt) {
            Id = Guid.NewGuid();
            SourceFilePath = sourcePath;
            Width = width;
            Height = height;
            SourceFileSize = sourceFileSize;
            SourceFileExt = sourceFileExt;
            StyleFilePath = styleImagePath;
            StyleName = styleName;
            StyleFileSize = styleFileSize;
            StyleFileExt = styleFileExt;
        }

        public async Task SetResultAsync(string resultPath) {
            ResultFilePath = resultPath;
            ResultFileExt = Path.GetExtension(resultPath);
            ResultFileSize = FileUtil.GetFileSize(resultPath);
            (var width, var height) = await FileUtil.GetImageResolutionAsync(resultPath);
            ResultResolution = $"{width} * {height}";
        }
    }
}
