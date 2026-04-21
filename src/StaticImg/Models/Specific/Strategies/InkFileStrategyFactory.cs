using System;
using VirtualPaper.Common;
using Workloads.Creation.StaticImg.Models.Specific.FileHandle;

namespace Workloads.Creation.StaticImg.Models.Specific.Strategies {
    public static class InkFileStrategyFactory {
        public static IInkFileStrategy GetStrategy(FileType fileType) {
            return fileType switch {
                FileType.FDesign => _designFileStrategy.Value,
                FileType.FImage => _imageFileStrategy.Value,
                _ => throw new NotSupportedException($"不支持的文件类型策略: {fileType}")
            };
        }

        private static readonly Lazy<DesignFileStrategy> _designFileStrategy = new Lazy<DesignFileStrategy>(() => new DesignFileStrategy());
        private static readonly Lazy<ImageFileStrategy> _imageFileStrategy = new Lazy<ImageFileStrategy>(() => new ImageFileStrategy());
    }
}
