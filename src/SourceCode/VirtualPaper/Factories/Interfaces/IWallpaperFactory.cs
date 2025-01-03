﻿using VirtualPaper.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Factories.Interfaces {
    public interface IWallpaperFactory {
        IWpPlayer CreatePlayer(
            IWpPlayerData data,
            IMonitor monitor,
            bool isPreview = false);
    }
}
