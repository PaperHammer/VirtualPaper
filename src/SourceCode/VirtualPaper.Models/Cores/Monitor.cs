using System.Drawing;
using System.Text.Json.Serialization;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    public class Monitor : IMonitor {
        [JsonIgnore]
        public bool IsStale { get; set; }

        #region Properties
        public string DeviceId { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public Rectangle WorkingArea { get; set; }
        //public string DeviceName { get; set; } = string.Empty;
        //public string MonitorName { get; set; } = string.Empty;
        //[JsonConverter(typeof(IntPtrJsonConverter))]
        //public IntPtr HMonitor { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public string ThumbnailPath { get; set; } = string.Empty;
        public bool HasWallpaper { get; set; }
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
