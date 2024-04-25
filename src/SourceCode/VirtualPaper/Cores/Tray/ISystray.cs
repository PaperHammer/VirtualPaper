﻿namespace VirtualPaper.Cores.Tray
{
    public interface ISystray : IDisposable
    {
        void ShowBalloonNotification(int timeout, string title, string msg);
    }
}
