namespace VirtualPaper.Common.Utils.Bridge.Base {
    public interface INoifyBridge {
        void ShowExp(Exception ex);
        void ShowCanceled();
        void ShowMsg(bool isNeedLocalizer, string keyOrMsg, InfoBarType infoBarType, string extraMsg = "", string key = "", bool isAllowDuplication = true);
        void CloseAndRemoveMsg(string key);
        void Loading(bool cancelEnable, bool progressbarEnable, CancellationTokenSource[]? cts = null);
        void Loaded(CancellationTokenSource[]? cts = null);
        void UpdateProgressbarValue(int curValue, int toltalValue);
        void ShowWarn(string msg);
    }
}
