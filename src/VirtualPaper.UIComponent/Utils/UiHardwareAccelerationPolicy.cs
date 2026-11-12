using System;
using System.IO;
using VirtualPaper.Common;

namespace VirtualPaper.UIComponent.Utils;

/// <summary>主 UI 启动时固定的加速策略，设置变更在下次启动生效。</summary>
public sealed class UiHardwareAccelerationPolicy(bool isEnabled) {
    public bool IsEnabled { get; } = isEnabled;

    // UI 与桌面播放器隔离；按模式分开目录，避免旧浏览器进程尚未退出时参数冲突。
    public string WebView2UserDataFolder { get; } = Path.Combine(
        Constants.CommonPaths.TempWebView2Dir, "UI", isEnabled ? "GPU" : "Software");

    public string WebView2BrowserArguments { get; } =
        "--disk-cache-size=1 --autoplay-policy=no-user-gesture-required" + (isEnabled ? "" : " --disable-gpu");

    public string GetWebView2UserDataFolder(string? partition = null) {
        if (partition == null) return WebView2UserDataFolder;
        if (string.IsNullOrWhiteSpace(partition) || partition is "." or ".."
            || Path.GetFileName(partition) != partition || partition.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("The WebView profile partition must be a single directory name.", nameof(partition));
        return Path.Combine(WebView2UserDataFolder, partition);
    }
}
