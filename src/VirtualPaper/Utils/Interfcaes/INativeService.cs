using VirtualPaper.Common.Utils.PInvoke;

namespace VirtualPaper.Utils.Interfcaes {
    public interface INativeService {
        nint CreateWorkerW();
        bool TrySetParentWorkerW(nint childHandle, nint parentHandle);
        bool SetWindowPos(nint handle, int hWndInsertAfter, int x, int y, int width, int height, int wFlags);
        void RefreshDesktop();
        nint GetWorkerWRect(out Native.RECT rect);
        bool MapWindowPoints(nint handle, nint workerW, ref Native.RECT rect, int cPoints);
        int SHQueryUserNotificationState(out Native.QUERY_USER_NOTIFICATION_STATE state);
        nint GetForegroundWindow();
        uint GetWindowThreadProcessId(nint hwnd, out int processId);
        string GetProcessNameById(int processId);
        void LockWorkStation();
    }
}
