using System.Windows.Threading;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Utils.Services {
    public class DispatcherTimerAdapter : IDispatcherTimer {
        private readonly DispatcherTimer _inner = new();
        public TimeSpan Interval {
            get => _inner.Interval;
            set => _inner.Interval = value;
        }
        public bool IsEnabled => _inner.IsEnabled;
        public event EventHandler? Tick {
            add => _inner.Tick += value;
            remove => _inner.Tick -= value;
        }
        public void Start() => _inner.Start();
        public void Stop() => _inner.Stop();
    }
}
