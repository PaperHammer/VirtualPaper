using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace VirtualPaper.UIComponent.Utils;

/// <summary>仅在 UI 线程创建 WebView 环境，同一分区复用初始化任务和启动参数。</summary>
public sealed class UiWebViewEnvironment(UiHardwareAccelerationPolicy policy) {
    private readonly Dictionary<string, Task<CoreWebView2Environment>> _environments = new(StringComparer.OrdinalIgnoreCase);

    public async Task<CoreWebView2Environment> CreateAsync(string? partition = null) {
        string folder = policy.GetWebView2UserDataFolder(partition);
        if (!_environments.TryGetValue(folder, out var task)) {
            task = CreateEnvironmentAsync(folder);
            _environments.Add(folder, task);
        }
        try { return await task; }
        catch {
            if (_environments.TryGetValue(folder, out var current) && ReferenceEquals(current, task))
                _environments.Remove(folder);
            throw;
        }
    }

    private async Task<CoreWebView2Environment> CreateEnvironmentAsync(string folder) =>
        await CoreWebView2Environment.CreateWithOptionsAsync(null, folder,
            new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = policy.WebView2BrowserArguments });
}
