﻿using System.IO;
using System.Text.Json.Serialization;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    [JsonSerializable(typeof(WpBasicData))]
    [JsonSerializable(typeof(IWpBasicData))]
    public partial class WpBasicDataContext : JsonSerializerContext { }

    public class WpBasicData : IWpBasicData {       
        public string WallpaperUid { get; set; } = string.Empty;
        public ApplicationInfo AppInfo { get; set; } = new();
        public string Title { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string Authors { get; set; } = string.Empty;
        public string PublishDate { get; set; } = string.Empty;
        public double Rating { get; set; } = -1;

        private FileType _fType;
        public FileType FType {
            get => _fType;
            set {
                _fType = value;
                this.IsSingleRType =
                     value switch {
                         FileType.FImage or FileType.FGif => false,
                         FileType.FVideo => true,
                         _ => false,
                     };
            }
        }
        public bool IsSingleRType { get; set; } = false;
        public string Partition { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public WallpaperStatus Status { get; set; }

        public string FolderName { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;

        public string Resolution { get; set; } = string.Empty;
        public string AspectRatio { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;

        public bool IsSubscribed { get; set; } = false;

        public IWpBasicData Clone() {
            IWpBasicData data = new WpBasicData() {
                WallpaperUid = this.WallpaperUid,
                AppInfo = this.AppInfo,
                Title = this.Title,
                Desc = this.Desc,
                Authors = this.Authors,
                PublishDate = this.PublishDate,
                Rating = this.Rating,
                FType = this.FType,
                IsSingleRType = this.IsSingleRType,
                Partition = this.Partition,
                Tags = this.Tags,

                FolderName = this.FolderName,
                FolderPath = this.FolderPath,
                FilePath = this.FilePath,
                ThumbnailPath = this.ThumbnailPath,

                Resolution = this.Resolution,
                AspectRatio = this.AspectRatio,
                FileSize = this.FileSize,
                FileExtension = this.FileExtension,

                IsSubscribed = this.IsSubscribed,
            };

            return data;
        }

        public void InitData(WpBasicData source) {
            if (source == null) return;

            this.WallpaperUid = source.WallpaperUid;
            this.AppInfo = source.AppInfo;
            this.Title = source.Title;
            this.Desc = source.Desc;
            this.Authors = source.Authors;
            this.PublishDate = source.PublishDate;
            this.Rating = source.Rating;
            this.FType = source.FType;
            this.IsSingleRType = source.IsSingleRType;
            this.Partition = source.Partition;
            this.Tags = source.Tags;

            this.FolderName = source.FolderName;
            this.FolderPath = source.FolderPath;
            this.FilePath = source.FilePath;
            this.ThumbnailPath = source.ThumbnailPath;

            this.Resolution = source.Resolution;
            this.AspectRatio = source.AspectRatio;
            this.FileSize = source.FileSize;
            this.FileExtension = source.FileExtension;

            this.IsSubscribed = source.IsSubscribed;
        }

        public void Read(string filePath) {
            var data = JsonSaver.Load<WpBasicData>(filePath, WpBasicDataContext.Default);
            InitData(data);
        }

        public void Save() {
            JsonSaver.Store<IWpBasicData>(
                Path.Combine(this.FolderPath, Constants.Field.WpBasicDataFileName), 
                this,
                WpBasicDataContext.Default);
        }

        public async Task MoveToAsync(string targetFolderPath) {
            if (!Directory.Exists(targetFolderPath)) {
                FileUtil.CopyDirectory(
                    this.FolderPath,
                    targetFolderPath,
                    true);
            }
            string oldFolderPath = this.FolderPath;

            if (oldFolderPath != targetFolderPath) {
                this.FolderPath = await FileUtil.UpdateFileFolderPathAsync(this.FolderPath, oldFolderPath, targetFolderPath);
                this.FilePath = await FileUtil.UpdateFileFolderPathAsync(this.FilePath, oldFolderPath, targetFolderPath);
                this.ThumbnailPath = await FileUtil.UpdateFileFolderPathAsync(this.ThumbnailPath, oldFolderPath, targetFolderPath);
            }

            Save();
        }

        public void Merge(IWpBasicData oldData) {
            if (oldData == null) return;

            this.Title = oldData.Title;
            this.Desc = oldData.Desc;
            this.Authors = oldData.Authors;
            this.PublishDate = oldData.PublishDate;
            this.Rating = oldData.Rating;
            this.IsSingleRType = oldData.IsSingleRType;
            this.Partition = oldData.Partition;
            this.Tags = oldData.Tags;
            this.IsSubscribed = oldData.IsSubscribed;
        }

        public bool IsAvailable() {
            return this.FType != FileType.FUnknown && this.WallpaperUid != string.Empty && this.AppInfo.AppVersion != string.Empty && this.ThumbnailPath != string.Empty;
        }

        public bool Equals(IWpBasicData? other) {
            return other != null && other.WallpaperUid == this.WallpaperUid && other.FilePath == this.FilePath;
        }
    }

    public enum WallpaperStatus {
        Locked,
        Normal,
        Auditing,        
        Deleted,
    }
}
