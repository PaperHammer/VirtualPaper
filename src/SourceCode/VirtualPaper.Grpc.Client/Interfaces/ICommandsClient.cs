namespace VirtualPaper.Grpc.Client.Interfaces
{
    public interface ICommandsClient
    {
        Task ScreensaverConfigure();
        Task ScreensaverPreview(int previewHandle);
        Task ScreensaverShow(bool show);
        Task ShowUI();
        Task CloseUI();
        Task RestartUI();
        Task ShowDebugView();
        Task ShutDown();
        void SaveRectUI();
        Task SaveRectUIAsync();
    }
}
