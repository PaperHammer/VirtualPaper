using Newtonsoft.Json;
using System.Drawing;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.Models.Cores
{
    public class Monitor : ObservableObject, IMonitor
    {
        [JsonIgnore]
        public bool IsStale { get; set; }

        #region Properties
        private string _deviceId = string.Empty;
        public string DeviceId
        {
            get => _deviceId;
            set { _deviceId = value; OnPropertyChanged(); }
        }

        private string _deviceName = string.Empty;
        public string DeviceName
        {
            get => _deviceName;
            set { _deviceName = value; OnPropertyChanged(); }
        }

        private string _monitorName = string.Empty;
        public string MonitorName
        {
            get => _monitorName;
            set { _monitorName = value; OnPropertyChanged(); }
        }

        private IntPtr _hMonitor = IntPtr.Zero;
        public IntPtr HMonitor
        {
            get => _hMonitor;
            set { _hMonitor = value; OnPropertyChanged(); }
        }

        private int _index;
        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(); }
        }

        private bool _isPrimary;
        public bool IsPrimary
        {
            get => _isPrimary;
            set { _isPrimary = value; OnPropertyChanged(); }
        }

        private Rectangle _bounds = Rectangle.Empty;
        public Rectangle Bounds
        {
            get => _bounds;
            set { _bounds = value; OnPropertyChanged(); }
        }

        private Rectangle _workingArea = Rectangle.Empty;
        public Rectangle WorkingArea
        {
            get => _workingArea;
            set { _workingArea = value; OnPropertyChanged(); }
        }
        #endregion

        public Monitor() { }

        public Monitor(string deviceName) => DeviceName = deviceName;

        public bool Equals(IMonitor? other)
        {
            return other != null && other.DeviceId == this.DeviceId;
        }
    }
}
