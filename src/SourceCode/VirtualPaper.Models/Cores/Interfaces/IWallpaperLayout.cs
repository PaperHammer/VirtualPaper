﻿using Windows.Devices.Display;

namespace VirtualPaper.Models.Cores.Interfaces
{
    /// <summary>
    /// 显示器所显示的内容路径
    /// </summary>
    public interface IWallpaperLayout
    {
        string FolderPath { get; set; }
        Monitor Monitor { get; set; }        
    }
}
