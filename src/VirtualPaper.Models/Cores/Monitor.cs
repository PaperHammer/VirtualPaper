using System.Drawing;
using System.Text.Json.Serialization;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.Models.Cores {
    [JsonSerializable(typeof(Monitor))]
    [JsonSerializable(typeof(IMonitor))]
    public partial class MonitorContext : JsonSerializerContext { }

    public partial class Monitor : ObservableObject, IMonitor {
        [JsonIgnore]
        public bool IsStale { get; set; }
        [JsonIgnore]
        public bool IsCloned { get; private set; }

        #region Properties
        public string DeviceId { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public Rectangle WorkingArea { get; set; }
        public string Content { get; set; } = "Monitor";
        public int SystemIndex { get; set; } = -1;
        public bool IsPrimary { get; set; }

        private string _thumbnailPath = string.Empty;
        public string ThumbnailPath {
            get => _thumbnailPath;
            set { _thumbnailPath = value; OnPropertyChanged(); }
        }
        #endregion

        public Monitor() {
        }

        public Monitor(string content) {
            Content = content;
        }

        public IMonitor CloneWithPrimaryInfo() {
            var monitor = new Monitor() {
                DeviceId = this.DeviceId,
                Content = this.Content,
                SystemIndex = this.SystemIndex,
                IsPrimary = this.IsPrimary,
                ThumbnailPath = this.ThumbnailPath,
                IsCloned = true,
            };
            return monitor;
        }

        public bool Equals(IMonitor? other) {
            return other != null && other.DeviceId == this.DeviceId;
        }
    }
}
