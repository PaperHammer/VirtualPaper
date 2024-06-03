using System.ComponentModel;
using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores;

namespace VirtualPaper.Models.WallpaperMetaData
{
    [Serializable]
    public class MetaData : IMetaData // ILibraryModel + ILivelyInfo
    {
        public string VirtualPaperUid { get; set; } = string.Empty; // online
        public ApplicationInfo AppInfo { get; set; } = new(); // apply online
        public string Title { get; set; } = string.Empty;// apply online
        public string Desc { get; set; } = string.Empty; // apply online
        public string Authors { get; set; } = string.Empty; // online
        public string PublishDate { get; set; } = string.Empty; // online
        public WallpaperType Type { get; set; }
        public string Partition { get; set; } = string.Empty; // online
        public string Tags { get; set; } = string.Empty; // online

        public string FolderPath { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty; // create
        public string WpCustomizePath { get; set; } = string.Empty;
        public string WpCustomizePathUsing { get; set; } = string.Empty;
        public string WpCustomizePathTmp { get; set; } = string.Empty;

        public string Resolution { get; set; } = string.Empty;
        public string AspectRatio { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;

        public RunningState State { get; set; } // local run
        public bool IsStartup { get; set; } // local run
        public bool IsSubscribed { get; set; } // local run
        public bool IsDownloading { get; set; } // local run
        public float DownloadingProgress { get; set; } // local run
        public string DownloadingProgressText { get; set; } = string.Empty; // local run
        //public List<string> Arguments { get; set; } = []; // local run

        public MetaData() { }

        public MetaData(IMetaData metaData)
        {
            Init(metaData);
        }

        public MetaData(string folderPath)
        {
            this.FolderPath = folderPath;
            string dataPath = Path.Combine(folderPath, "wpMetaData.json");

            var metaData = JsonStorage<MetaData>.LoadData(dataPath);
            Init(metaData);
        }

        private void Init(IMetaData metaData)
        {
            this.VirtualPaperUid = metaData.VirtualPaperUid;
            this.AppInfo = metaData.AppInfo;
            this.Title = metaData.Title;
            this.Desc = metaData.Desc;
            this.Authors = metaData.Authors;
            this.PublishDate = metaData.PublishDate;
            this.Type = metaData.Type;
            this.Partition = metaData.Partition;
            this.Tags = metaData.Tags;

            this.FolderPath = metaData.FolderPath;
            this.FilePath = metaData.FilePath;
            this.ThumbnailPath = metaData.ThumbnailPath;
            this.WpCustomizePath = metaData.WpCustomizePath;
            this.WpCustomizePathUsing = metaData.WpCustomizePathUsing;
            this.WpCustomizePathTmp = metaData.WpCustomizePathTmp;

            this.Resolution = metaData.Resolution;
            this.AspectRatio = metaData.AspectRatio;
            this.FileSize = metaData.FileSize;
            this.FileExtension = metaData.FileExtension;

            this.State = metaData.State;
            this.IsStartup = metaData.IsStartup;
            this.IsSubscribed = metaData.IsSubscribed;
            this.IsDownloading = metaData.IsDownloading;
            this.DownloadingProgress = metaData.DownloadingProgress;
            this.DownloadingProgressText = metaData.DownloadingProgressText;
        }

        public enum RunningState
        {
            [Description("Importing..")]
            processing,
            [Description("Import complete.")]
            ready,
            cmdImport,
            multiImport,
            edit,
            gallery,
        }
    }
}
