﻿using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    public class WpPlayerData : IWpPlayerData {
        public string WallpaperUid { get; set; } = string.Empty;
        public RuntimeType RType { get; set; } = RuntimeType.RUnknown;
        public string FilePath { get; set; } = string.Empty;
        public string DepthFilePath { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public string WpEffectFilePathTemplate { get; set; } = string.Empty;
        public string WpEffectFilePathTemporary { get; set; } = string.Empty;
        public string WpEffectFilePathUsing { get; set; } = string.Empty;

        public IWpMetadata GetMetadata() {
            WpMetadata metadata = new() {
                BasicData = JsonStorage<WpBasicData>.LoadData(Path.Combine(FolderPath, Constants.Field.WpBasicDataFileName)),
                RuntimeData = JsonStorage<WpRuntimeData>.LoadData(Path.Combine(FolderPath, Constants.Field.WpRuntimeDataFileName)),
            };

            return metadata;
        }
    }
}