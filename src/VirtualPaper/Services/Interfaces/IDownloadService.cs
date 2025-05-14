namespace VirtualPaper.Services.Interfaces {
    public interface IDownloadService {
        event EventHandler<DownloadCompletedEventArgs> DownloadFileCompleted;
        event EventHandler<DownloadProgressEventArgs> DownloadProgressChanged;
        event EventHandler<DownloadEventArgs> DownloadStarted;

        Task DownloadFile(Uri url, string filePath);
        void Cancel();
    }

    public class DownloadProgressEventArgs : EventArgs {
        /// <summary>
        /// Total size of file in megabytes.
        /// </summary>
        public double TotalSize { get; set; }
        /// <summary>
        /// Currently downloaded file size in megabytes.
        /// </summary>
        public double DownloadedSize { get; set; }
        /// <summary>
        /// Download progress.
        /// </summary>
        public double Percentage { get; set; }
    }

    public class DownloadEventArgs : EventArgs {
        /// <summary>
        /// Total size of file in megabytes.
        /// </summary>
        public double TotalSize { get; set; }
        /// <summary>
        /// Name of the file.
        /// </summary>
        public string FileName { get; set; } = string.Empty;
    }

    public class DownloadCompletedEventArgs : EventArgs {
        public bool IsCompleted { get; set; }
        public bool IsNormal { get; set; }
        public string Msg { get; set; } = string.Empty;
    }
}
