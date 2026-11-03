using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Service.Commands;
using VirtualPaper.Grpc.Service.UserSettings;
using VirtualPaper.Models.Cores;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal sealed class AppProcessSession : IAsyncDisposable {
    private Process? _core;
    private Process? _ui;
    private readonly List<int> _uiIds = [];
    private int _showUiRequests;
    private readonly Dictionary<string, int> _previousLogLines;
    private readonly Grpc_CommandsService.Grpc_CommandsServiceClient _commands;
    public Grpc_UserSettingsService.Grpc_UserSettingsServiceClient Settings { get; }
    public int CoreId => _core!.Id;
    public IReadOnlyList<int> UiIds => _uiIds;
    public string SettingsPath => Constants.CommonPaths.UserSettingsPath;
    public Process? FindChild(string executable) => FindChild(_core!.Id, executable);

    public AppProcessSession() {
        if (!TestProcessEnvironment.IsEnabled) throw new InvalidOperationException("A process test session is required.");
        UiTestContext.Check(!Constants.IsTestMode, "Real UI process tests must not enable unit-test behavior overrides.");
        UiTestContext.Check(string.Equals(Constants.CommonPaths.AppDataDir, TestProcessEnvironment.DataRoot,
            StringComparison.OrdinalIgnoreCase), "Real UI process tests must retain the isolated data root.");
        _previousLogLines = Directory.Exists(Constants.CommonPaths.LogDir)
            ? Directory.EnumerateFiles(Constants.CommonPaths.LogDir, "*.txt", SearchOption.AllDirectories)
                .ToDictionary(path => path, path => File.ReadLines(path).Count())
            : [];
        var channel = new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName);
        Settings = new(channel);
        _commands = new(channel);
    }

    public async Task StartAsync(Action<Settings>? configure = null) {
        string coreExe = Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_CORE_EXE")
            ?? throw new InvalidOperationException("Core executable path is missing.");
        Directory.CreateDirectory(Constants.CommonPaths.AppDataDir);
        var settings = new Settings {
            IsFirstRun = false, IsUpdated = false, IsAutoStart = false, IsScreenSaverOn = false,
            NavigationAnimationsEnabled = true, Language = "en-US"
        };
        configure?.Invoke(settings);
        Directory.CreateDirectory(settings.WallpaperDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, SettingsContext.Default.Settings));
        File.WriteAllText(Constants.CommonPaths.AppRulesPath, "[]");
        File.WriteAllText(Constants.CommonPaths.WallpaperLayoutPath, "[]");
        File.WriteAllText(Constants.CommonPaths.RecentUsedPath, "[]");
        _core = Process.Start(new ProcessStartInfo(coreExe) {
            WorkingDirectory = Path.GetDirectoryName(coreExe)!, UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        }) ?? throw new InvalidOperationException("Core process could not start.");

        await WaitAsync(async () => {
            if (_core.HasExited) throw new InvalidOperationException($"Core exited during startup: {_core.ExitCode}");
            try {
                await Settings.GetSettingsAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1));
                return true;
            }
            catch (global::Grpc.Core.RpcException) { return false; }
        }, "Core settings RPC readiness");
    }

    public async Task<Process> OpenUiAsync() {
        await _commands.ShowUIAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(10));
        _showUiRequests++;
        var lastRequest = Stopwatch.StartNew();
        await WaitAsync(async () => {
            if (_core!.HasExited) throw new InvalidOperationException("Core exited before UI became ready.");
            _ui ??= FindChild(_core.Id, "VirtualPaper.UI.exe");
            if (_ui == null) {
                // OS process exit can precede Core's Exited callback cleanup.
                // ShowUI is idempotent; retry within readiness timeout rather
                // than treating its first acknowledgement as a ready window.
                if (lastRequest.ElapsedMilliseconds >= 250) {
                    await _commands.ShowUIAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(10));
                    _showUiRequests++;
                    lastRequest.Restart();
                }
                return false;
            }
            if (_ui.HasExited) throw new InvalidOperationException($"UI exited during startup: {_ui.ExitCode}");
            _ = _ui.Handle; // Retain an exit-query handle before settings can restart the UI.
            _ui.Refresh();
            return _ui.MainWindowHandle != IntPtr.Zero;
        }, "Real UI process window");
        _uiIds.Add(_ui!.Id);
        return _ui;
    }

    public async Task CloseUiAsync() {
        if (_ui == null) return;
        // The production UI handles WM_CLOSE, checks pages, then exits normally.
        UiTestContext.Check(_ui.CloseMainWindow(), "UI did not accept a window-close request");
        await _ui.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        UiTestContext.Check(_ui.ExitCode == 0, $"UI exited abnormally: {_ui.ExitCode}");
        _ui.Dispose();
        _ui = null;
    }

    public async Task<Process> WaitForUiRestartAsync() {
        if (_ui == null) throw new InvalidOperationException("No UI process is being tracked for restart.");
        int previousId = _ui.Id;
        await _ui.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        UiTestContext.Check(_ui.ExitCode == 0, $"UI exited abnormally during automatic restart: {_ui.ExitCode}");
        _ui.Dispose();
        _ui = null;
        // Observe Core's automatic restart; do not send ShowUI to create the
        // replacement on behalf of the production settings workflow.
        await WaitAsync(() => {
            if (_core!.HasExited) throw new InvalidOperationException("Core exited during automatic UI restart.");
            _ui ??= FindChild(_core.Id, "VirtualPaper.UI.exe");
            if (_ui == null) return Task.FromResult(false);
            UiTestContext.Check(_ui.Id != previousId && !_ui.HasExited, "Core did not create a new live UI process");
            _ = _ui.Handle;
            _ui.Refresh();
            return Task.FromResult(_ui.MainWindowHandle != 0);
        }, "Core automatically restarted UI");
        _uiIds.Add(_ui!.Id);
        return _ui;
    }

    public async Task StopAsync() {
        await CloseUiAsync();
        await _commands.ShutDownAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(5));
        await _core!.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        UiTestContext.Check(_core.ExitCode == 0, $"Core exited abnormally: {_core.ExitCode}");
    }

    public static async Task WaitAsync(Func<Task<bool>> condition, string operation, TimeSpan? timeout = null) {
        var timer = Stopwatch.StartNew();
        while (!await condition()) {
            if (timer.Elapsed > (timeout ?? TimeSpan.FromSeconds(15))) throw new TimeoutException(operation);
            await Task.Delay(100);
        }
    }

    public static async Task<JsonNode> WaitForJsonAsync(string path, Func<JsonNode, bool> matches) {
        JsonNode? saved = null;
        await WaitAsync(async () => {
            try {
                saved = JsonNode.Parse(await ReadTextSharedAsync(path));
                return saved != null && matches(saved);
            }
            // The RPC can expose the in-memory change before its save has finished.
            // Retry a locked or incomplete file, but still require the persisted values.
            catch (IOException) { return false; }
            catch (JsonException) { return false; }
        }, $"Persisted JSON: {path}");
        return saved!;
    }

    public static async Task<string> ReadTextSharedAsync(string path) {
        // Observe atomic saves without preventing the application from replacing the file.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public void RecordEvidence(UiTestContext context) {
        UiTestContext.Check(_core is { HasExited: true } && _ui == null, "Session processes have not exited normally");
        context.Evidence["CoreProcessId"] = CoreId;
        context.Evidence["SessionId"] = TestProcessEnvironment.SessionId;
        context.Evidence["UiProcessIds"] = UiIds.ToArray();
        context.Evidence["ShowUiRequests"] = _showUiRequests;
        context.Evidence["ProcessesExitedNormally"] = true;
        context.Evidence["UnitTestBehaviorOverridesEnabled"] = Constants.IsTestMode;
        context.Evidence["RuntimeErrors"] = Directory.EnumerateFiles(Constants.CommonPaths.LogDir, "*.txt", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path).Skip(_previousLogLines.GetValueOrDefault(path)))
            .Where(line => line.Contains("|ERROR|", StringComparison.Ordinal)).Distinct().ToArray();
    }

    public async ValueTask DisposeAsync() {
        try {
            if (_ui is { HasExited: false }) {
                _ui.CloseMainWindow();
                try { await _ui.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (TimeoutException) {
                    _ui.Kill(entireProcessTree: true);
                    await _ui.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                }
            }
            if (_core is { HasExited: false }) {
                try {
                    await _commands.ShutDownAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(2));
                    await _core.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                }
                catch {
                    if (!_core.HasExited) _core.Kill(entireProcessTree: true);
                    await _core.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                }
            }
        }
        finally { _ui?.Dispose(); _core?.Dispose(); }
    }

    // Select only a direct child of the Core process started by this session, never by name alone.
    private static Process? FindChild(int parentId, string executable) {
        nint snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == -1) throw new System.ComponentModel.Win32Exception();
        try {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            if (!Process32First(snapshot, ref entry)) return null;
            do {
                if (entry.ParentId == parentId && entry.Exe.Equals(executable, StringComparison.OrdinalIgnoreCase)) {
                    try {
                        var process = Process.GetProcessById((int)entry.Id);
                        _ = process.Handle;
                        if (!process.HasExited) return process;
                        process.Dispose();
                    }
                    catch (ArgumentException) { } // It exited after the process snapshot was taken.
                    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 87) { }
                }
            } while (Process32Next(snapshot, ref entry));
            return null;
        }
        finally { CloseHandle(snapshot); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry {
        public uint Size, Usage, Id;
        public nuint Heap;
        public uint ModuleId, Threads, ParentId;
        public int BasePriority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32First(nint snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32Next(nint snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}
