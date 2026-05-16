using System;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.IntelligentPanel.Models {
    public class SuperResolutionData : IIntelliData {
        public Guid Id { get; } = Guid.NewGuid();

        public string SourceFilePath { get; }
        public string SourceFileSize { get; }
        public string SourceFileExt { get; }
        public uint Width { get; }
        public uint Height { get; }

        /// <summary>
        /// 增强模式
        /// </summary>
        public EnhanceMode Mode { get; }

        /// <summary>
        /// 放大倍率（QualityRestore 模式为 1）
        /// </summary>
        public int Magnification { get; }

        /// <summary>
        /// 目标宽度
        /// </summary>
        public uint TargetWidth { get; }

        /// <summary>
        /// 目标高度
        /// </summary>
        public uint TargetHeight { get; }

        // ── 结果 ──
        public string? ResultFilePath { get; private set; }
        public string? ResultResolution { get; private set; }
        public string? ResultFileSize { get; private set; }
        public string? ResultFileExt { get; private set; }

        public SuperResolutionData(
            string sourceFilePath,
            string sourceFileSize,
            string sourceFileExt,
            uint width,
            uint height,
            EnhanceMode mode,
            int magnification,
            uint targetWidth,
            uint targetHeight) {
            SourceFilePath = sourceFilePath;
            SourceFileSize = sourceFileSize;
            SourceFileExt = sourceFileExt;
            Width = width;
            Height = height;
            Mode = mode;
            Magnification = magnification;
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
            ResultResolution = $"{TargetWidth} * {TargetHeight}";
            ResultFileExt = SourceFileExt;
        }

        public void SetResult(string resultFilePath) {
            ResultFilePath = resultFilePath;
            ResultFileSize = FileUtil.GetFileSize(resultFilePath);
        }
    }
}