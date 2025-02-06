namespace VirtualPaper.Common.Utils.Bridge.Base {
    public interface INoifyBridge {
        void ShowExp(Exception ex);
        void ShowCanceled();
        void ShowMsg(bool isNeedLocalizer, string msg, InfoBarType infoBarType);
        void Loading(bool cancelEnable, bool progressbarEnable, CancellationTokenSource[] cts);
        void Loaded(CancellationTokenSource[] cts);
        void UpdateProgressbarValue(int curValue, int toltalValue);
    }
}
