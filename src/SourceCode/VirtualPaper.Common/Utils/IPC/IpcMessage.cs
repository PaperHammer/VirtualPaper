using VirtualPaper.Common.Models;

namespace VirtualPaper.Common.Utils.IPC
{
    [Serializable]
    public abstract class IpcMessage(MessageType type)
    {
        public MessageType Type { get; } = type;
    }

    public enum MessageType
    {
        msg_hwnd,
        msg_console,
        msg_wploaded,
        msg_screenshot,

        cmd_reload,
        cmd_close,
        cmd_screenshot,
        cmd_suspend, // 挂起
        cmd_resume, // 恢复
        cmd_muted, // 恢复
        //cmd_volume,
        //lsp_perfcntr,
        //lsp_nowplaying,

        vp_slider,
        vp_textbox,
        vp_dropdown,
        vp_button,
        vp_cpicker,
        vp_chekbox,
    }

    public enum ConsoleMessageType
    {
        Log,
        Error,
        Console
    }

    public enum ScreenshotFormat
    {
        jpeg,
        png,
        webp,
        bmp
    }

    [Serializable]
    public class VirtualPaperMessageConsole : IpcMessage
    {
        public string Message { get; set; } = string.Empty;
        public ConsoleMessageType MsgType { get; set; }
        public VirtualPaperMessageConsole() : base(MessageType.msg_console) { }
    }

    [Serializable]
    public class VirtualPaperMessageHwnd : IpcMessage
    {
        public long Hwnd { get; set; }
        public VirtualPaperMessageHwnd() : base(MessageType.msg_hwnd) { }
    }

    [Serializable]
    public class VirtualPaperMessageScreenshot : IpcMessage
    {
        public string FileName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public VirtualPaperMessageScreenshot() : base(MessageType.msg_screenshot) { }
    }

    [Serializable]
    public class VirtualPaperMessageWallpaperLoaded : IpcMessage
    {
        public bool Success { get; set; }
        public VirtualPaperMessageWallpaperLoaded() : base(MessageType.msg_wploaded) { }
    }

    [Serializable]
    public class VirtualPaperCloseCmd : IpcMessage
    {
        public VirtualPaperCloseCmd() : base(MessageType.cmd_close) { }
    }

    [Serializable]
    public class VirtualPaperReloadCmd : IpcMessage
    {
        public VirtualPaperReloadCmd() : base(MessageType.cmd_reload) { }
    }

    [Serializable]
    public class VirtualPaperScreenshotCmd : IpcMessage
    {
        public ScreenshotFormat Format { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public uint Delay { get; set; }
        public VirtualPaperScreenshotCmd() : base(MessageType.cmd_screenshot) { }
    }

    [Serializable]
    public class VirtualPaperSuspendCmd : IpcMessage
    {
        public VirtualPaperSuspendCmd() : base(MessageType.cmd_suspend) { }
    }

    [Serializable]
    public class VirtualPaperResumeCmd : IpcMessage
    {
        public VirtualPaperResumeCmd() : base(MessageType.cmd_resume) { }
    }
    
    [Serializable]
    public class VirtualPaperMuted : IpcMessage
    {
        public VirtualPaperMuted() : base(MessageType.cmd_muted) { }

        public bool IsMuted { get; set; }
    }

    //[Serializable]
    //public class VirtualPaperVolumeCmd : IpcMessage
    //{
    //    public int Volume { get; set; }
    //    public VirtualPaperVolumeCmd() : base(MessageType.cmd_volume) { }
    //}

    [Serializable]
    public class VirtualPaperSystemInformation : IpcMessage
    {
        public HardwareUsageEventArgs? Info { get; set; }
        public VirtualPaperSystemInformation() : base(MessageType.cmd_reload) { }
    }

    //[Serializable]
    //public class VirtualPaperSystemNowPlaying : IpcMessage
    //{
    //    public NowPlayingEventArgs? Info { get; set; }
    //    public VirtualPaperSystemNowPlaying() : base(MessageType.lsp_nowplaying) { }
    //}

    [Serializable]
    public class VirtualPaperSlider : IpcMessage
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Step { get; set; }
        public VirtualPaperSlider() : base(MessageType.vp_slider) { }
    }

    [Serializable]
    public class VirtualPaperTextBox : IpcMessage
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public VirtualPaperTextBox() : base(MessageType.vp_textbox) { }
    }

    [Serializable]
    public class VirtualPaperDropdown : IpcMessage
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public VirtualPaperDropdown() : base(MessageType.vp_dropdown) { }
    }

    //[Serializable]
    //public class VirtualPaperFolderDropdown : IpcMessage
    //{
    //    public string Name { get; set; } = string.Empty;
    //    public string Value { get; set; } = string.Empty;
    //    public VirtualPaperFolderDropdown() : base(MessageType.lp_fdropdown) { }
    //}

    [Serializable]
    public class VirtualPaperCheckbox : IpcMessage
    {
        public string Name { get; set; } = string.Empty;
        public bool Value { get; set; }
        public VirtualPaperCheckbox() : base(MessageType.vp_chekbox) { }
    }

    [Serializable]
    public class VirtualPaperColorPicker : IpcMessage
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public VirtualPaperColorPicker() : base(MessageType.vp_cpicker) { }
    }

    [Serializable]
    public class VirtualPaperButton : IpcMessage
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public VirtualPaperButton() : base(MessageType.vp_button) { }
    }
}
