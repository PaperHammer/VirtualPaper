using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal sealed class UiTestContext : IDisposable {
    private readonly Window _window;
    private Page? _page;
    private Exception? _unhandled;
    private Windows.Graphics.SizeInt32? _initialWindowSize;
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "VirtualPaper.UIOperationTest", Guid.NewGuid().ToString("N"));
    public Dictionary<string, object?> Evidence { get; } = [];

    public UiTestContext(Window window) {
        _window = window;
        Directory.CreateDirectory(Root);
        Application.Current.UnhandledException += OnUnhandled;
    }

    private void OnUnhandled(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args) {
        _unhandled ??= args.Exception;
        args.Handled = true;
    }

    public async Task<TPage> OpenAsync<TPage>(FrameworkPayload payload) where TPage : Page {
        (_page as IDisposable)?.Dispose();
        var frame = new Frame();
        _window.Content = frame;
        Check(frame.Navigate(typeof(TPage), payload), "Page navigation failed");
        _page = (TPage)frame.Content;
        _window.Activate();
        nint window = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        try {
            await ApplicationUi.ActivateOwnedWindowAsync(window);
            await WaitAsync(() => _page.IsLoaded && _page.ActualWidth > 0 && _page.ActualHeight > 0
                && GetForegroundWindow() == window, "Activate and lay out test window");
        }
        catch (TimeoutException) {
            Evidence["HostWindowState"] = new {
                Window = window.ToInt64(), Foreground = GetForegroundWindow().ToInt64(),
                _page.IsLoaded, _page.ActualWidth, _page.ActualHeight
            };
            SaveFailureSnapshot("host-activation-timeout");
            throw;
        }
        await WaitForLayoutAsync();
        return (TPage)_page;
    }

    public async Task WaitForLayoutAsync() {
        bool rendered = false;
        void OnRendering(object? sender, object args) => rendered = true;
        CompositionTarget.Rendering += OnRendering;
        try { await WaitAsync(() => rendered, "Render test window layout"); }
        finally { CompositionTarget.Rendering -= OnRendering; }
    }

    public async Task ResizeAsync(int width, int height) {
        _initialWindowSize ??= _window.AppWindow.Size;
        double scale = _page?.XamlRoot?.RasterizationScale ?? 1;
        var size = new Windows.Graphics.SizeInt32((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));
        _window.AppWindow.Resize(size);
        await WaitAsync(() => _window.AppWindow.Size.Width == size.Width && _window.AppWindow.Size.Height == size.Height,
            "Resize owned layout window");
        await WaitForLayoutAsync();
    }

    public void SaveFailureSnapshot(string name) {
        string path = Path.Combine(VirtualPaper.Common.Constants.CommonPaths.LogDir, "UIOperation", name + ".png");
        WindowSnapshot.Save(WinRT.Interop.WindowNative.GetWindowHandle(_window), path);
        Evidence["HostFailureScreenshot"] = path;
    }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();

    public async Task WaitAsync(Func<bool> condition, string operation = "UI operation") {
        var timer = Stopwatch.StartNew();
        while (true) {
            await Task.Yield();
            if (_unhandled != null) throw new InvalidOperationException("Unhandled UI exception", _unhandled);
            if (condition()) return;
            if (timer.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException($"{operation} did not complete");
            await Task.Delay(20);
        }
    }

    public static T Pattern<T>(FrameworkElement control, PatternInterface pattern) where T : class {
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(control)
            ?? throw new InvalidOperationException($"No automation peer for {control.Name}");
        return peer.GetPattern(pattern) as T
            ?? throw new InvalidOperationException($"{control.Name} does not expose {pattern}");
    }

    public static void Invoke(Button button) => Pattern<IInvokeProvider>(button, PatternInterface.Invoke).Invoke();
    public static void Check(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }

    public void Dispose() {
        try {
            (_page as IDisposable)?.Dispose();
            _window.Content = null;
            if (_initialWindowSize is { } size) _window.AppWindow.Resize(size);
            if (_unhandled != null) throw new InvalidOperationException("Unhandled UI exception", _unhandled);
        }
        finally {
            Application.Current.UnhandledException -= OnUnhandled;
            Directory.Delete(Root, recursive: true);
        }
    }
}
