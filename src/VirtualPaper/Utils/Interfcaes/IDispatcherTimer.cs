namespace VirtualPaper.Utils.Interfcaes {
    public interface IDispatcherTimer {
        TimeSpan Interval { get; set; }
        bool IsEnabled { get; }
        event EventHandler? Tick;
        void Start();
        void Stop();
    }
}
