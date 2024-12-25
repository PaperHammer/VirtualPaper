using System.Text.Json.Serialization;
using VirtualPaper.Common.Models;

namespace VirtualPaper.Common.Utils.IPC {
    [Serializable]
    // ref: https://learn.microsoft.com/zh-cn/dotnet/standard/serialization/system-text-json/polymorphism?pivots=dotnet-8-0
    [JsonDerivedType(typeof(VirtualPaperUpdateCmd), "cmd_update")]
    [JsonDerivedType(typeof(VirtualPaperMessageConsole), "msg_console")]
    [JsonDerivedType(typeof(VirtualPaperMessageHwnd), "msg_hwnd")]
    [JsonDerivedType(typeof(VirtualPaperMessageProcId), "msg_procid")]
    [JsonDerivedType(typeof(VirtualPaperMessageScreenshot), "msg_screenshot")]
    [JsonDerivedType(typeof(VirtualPaperMessageWallpaperLoaded), "msg_wploaded")]
    [JsonDerivedType(typeof(VirtualPaperCloseCmd), "cmd_close")]
    [JsonDerivedType(typeof(VirtualPaperReloadCmd), "cmd_reload")]
    [JsonDerivedType(typeof(VirtualPaperScreenshotCmd), "cmd_screenshot")]
    [JsonDerivedType(typeof(VirtualPaperApplyCmd), "cmd_apply")]
    [JsonDerivedType(typeof(VirtualPaperActiveCmd), "cmd_active")]
    [JsonDerivedType(typeof(VirtualPaperSuspendCmd), "cmd_suspend")]
    [JsonDerivedType(typeof(VirtualPaperResumeCmd), "cmd_resume")]
    [JsonDerivedType(typeof(VirtualPaperParallaxSuspendCmd), "cmd_suspend_parallax")]
    [JsonDerivedType(typeof(VirtualPaperParallaxResumeCmd), "cmd_resume_parallax")]
    [JsonDerivedType(typeof(VirtualPaperMutedCmd), "cmd_muted")]
    [JsonDerivedType(typeof(VirtualPaperSystemInformation), "msg_info")]
    [JsonDerivedType(typeof(VirtualPaperSlider), "vp_slider")]
    [JsonDerivedType(typeof(VirtualPaperTextBox), "vp_textbox")]
    [JsonDerivedType(typeof(VirtualPaperDropdown), "vp_dropdown")]
    [JsonDerivedType(typeof(VirtualPaperCheckbox), "vp_chekbox")]
    [JsonDerivedType(typeof(VirtualPaperColorPicker), "vp_cpicker")]
    [JsonDerivedType(typeof(VirtualPaperButton), "vp_button")]
    public abstract class IpcMessage(MessageType type) {
        public MessageType Type { get; } = type;
    }

    public enum MessageType {
        msg_hwnd,
        msg_procid,
        msg_console,
        msg_wploaded,
        msg_screenshot,
        msg_info,

        cmd_apply,
        cmd_active,
        cmd_reload,
        cmd_close,
        cmd_screenshot,
        cmd_suspend, // 挂起(Pause)
        cmd_resume, // 恢复(Play)
        cmd_muted,
        cmd_update,
        cmd_suspend_parallax,
        cmd_resume_parallax,

        vp_slider,
        vp_textbox,
        vp_dropdown,
        vp_button,
        vp_cpicker,
        vp_chekbox,
    }

    public enum ConsoleMessageType {
        Log,
        Error,
        Console
    }

    public enum ScreenshotFormat {
        jpeg,
        png,
        webp,
        bmp
    }

    [Serializable]
    public class VirtualPaperUpdateCmd : IpcMessage {
        public string FilePath { get; set; } = string.Empty;
        public string WpEffectFilePathUsing { get; set; } = string.Empty;
        public string WpType { get; set; } = string.Empty;
        public VirtualPaperUpdateCmd() : base(MessageType.cmd_update) { }
    }

    [Serializable]
    public class VirtualPaperMessageConsole : IpcMessage {
        public string Message { get; set; } = string.Empty;
        public ConsoleMessageType MsgType { get; set; }
        public VirtualPaperMessageConsole() : base(MessageType.msg_console) { }
    }

    [Serializable]
    public class VirtualPaperMessageHwnd : IpcMessage {
        public long Hwnd { get; set; }
        public VirtualPaperMessageHwnd() : base(MessageType.msg_hwnd) { }
    }

    [Serializable]
    public class VirtualPaperMessageProcId : IpcMessage {
        public long ProcId { get; set; }
        public VirtualPaperMessageProcId() : base(MessageType.msg_procid) { }
    }

    [Serializable]
    public class VirtualPaperMessageScreenshot : IpcMessage {
        public string FileName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public VirtualPaperMessageScreenshot() : base(MessageType.msg_screenshot) { }
    }

    [Serializable]
    public class VirtualPaperMessageWallpaperLoaded : IpcMessage {
        public bool Success { get; set; }
        public VirtualPaperMessageWallpaperLoaded() : base(MessageType.msg_wploaded) { }
    }

    [Serializable]
    public class VirtualPaperCloseCmd : IpcMessage {
        public VirtualPaperCloseCmd() : base(MessageType.cmd_close) { }
    }

    [Serializable]
    public class VirtualPaperReloadCmd : IpcMessage {
        public VirtualPaperReloadCmd() : base(MessageType.cmd_reload) { }
    }

    [Serializable]
    public class VirtualPaperScreenshotCmd : IpcMessage {
        public ScreenshotFormat Format { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public uint Delay { get; set; }
        public VirtualPaperScreenshotCmd() : base(MessageType.cmd_screenshot) { }
    }

    [Serializable]
    public class VirtualPaperApplyCmd : IpcMessage {
        public VirtualPaperApplyCmd() : base(MessageType.cmd_apply) { }
    }
    
    [Serializable]
    public class VirtualPaperActiveCmd : IpcMessage {
        public VirtualPaperActiveCmd() : base(MessageType.cmd_active) { }
    }

    [Serializable]
    public class VirtualPaperSuspendCmd : IpcMessage {
        public VirtualPaperSuspendCmd() : base(MessageType.cmd_suspend) { }
    }

    [Serializable]
    public class VirtualPaperResumeCmd : IpcMessage {
        public VirtualPaperResumeCmd() : base(MessageType.cmd_resume) { }
    }

    [Serializable]
    public class VirtualPaperParallaxSuspendCmd : IpcMessage {
        public VirtualPaperParallaxSuspendCmd() : base(MessageType.cmd_suspend_parallax) { }
    }

    [Serializable]
    public class VirtualPaperParallaxResumeCmd : IpcMessage {
        public VirtualPaperParallaxResumeCmd() : base(MessageType.cmd_resume_parallax) { }
    }

    [Serializable]
    public class VirtualPaperMutedCmd : IpcMessage {
        public VirtualPaperMutedCmd() : base(MessageType.cmd_muted) { }

        public bool IsMuted { get; set; }
    }

    [Serializable]
    public class VirtualPaperSystemInformation : IpcMessage {
        public HardwareUsageEventArgs? Info { get; set; }
        public VirtualPaperSystemInformation() : base(MessageType.msg_info) { }
    }

    [Serializable]
    public class VirtualPaperSlider : IpcMessage {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public VirtualPaperSlider() : base(MessageType.vp_slider) { }
    }

    [Serializable]
    public class VirtualPaperTextBox : IpcMessage {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public VirtualPaperTextBox() : base(MessageType.vp_textbox) { }
    }

    [Serializable]
    public class VirtualPaperDropdown : IpcMessage {
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
    public class VirtualPaperCheckbox : IpcMessage {
        public string Name { get; set; } = string.Empty;
        public bool Value { get; set; }
        public VirtualPaperCheckbox() : base(MessageType.vp_chekbox) { }
    }

    [Serializable]
    public class VirtualPaperColorPicker : IpcMessage {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public VirtualPaperColorPicker() : base(MessageType.vp_cpicker) { }
    }

    [Serializable]
    public class VirtualPaperButton : IpcMessage {
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public VirtualPaperButton() : base(MessageType.vp_button) { }
    }
}
