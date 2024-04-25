namespace VirtualPaper.Services.Interfaces
{
    /// <summary>
    /// 主 UI 设置
    /// </summary>
    public interface IUIRunnerService : IDisposable
    {
        void ShowUI();
        void CloseUI();
        void RestartUI();
        bool IsVisibleUI { get; }
        void SaveRectUI();
    }
}
