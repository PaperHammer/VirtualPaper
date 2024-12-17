using System.Drawing;
using System.Text.Json.Serialization;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.Models.Cores {
    public partial class Monitor : ObservableObject, IMonitor {
        [JsonIgnore]
        public bool IsStale { get; set; }

        #region Properties
        public string DeviceId { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public Rectangle WorkingArea { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }

        private string _thumbnailPath = string.Empty;
        public string ThumbnailPath {
            get => _thumbnailPath;
            set {
                _thumbnailPath = value;
                OnPropertyChanged();
            }
        }
        #endregion

        public Monitor() { }

        public Monitor(string content = "-1") {
            Content = content;
        }

        public bool Equals(IMonitor? other) {
            return other != null && other.DeviceId == this.DeviceId;
        }
    }
}
