namespace VirtualPaper.Services.Interfaces
{
    /// <summary>
    /// 屏幕保护服务
    /// </summary>
    public interface IScreensaverService
    {
        bool IsRunning { get; }
        void CreatePreview(IntPtr hwnd);
        void Start();
        void StartIdleTimer(uint idleTime);
        void Stop();
        void StopIdleTimer();
    }
}
