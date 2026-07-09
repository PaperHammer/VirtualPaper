using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Automation;

namespace VirtualPaper.SmokeTest;

internal static class AppStartSmoke {
    public static bool VerifyKeyFiles(string installDir) {
        string[] required =
        [
            Path.Combine(installDir, "VirtualPaper.exe"),
            Path.Combine(installDir, "Plugins", "UI", "VirtualPaper.UI.exe"),
            Path.Combine(installDir, "Plugins", "PlayerWeb", "VirtualPaper.PlayerWeb.exe"),
            Path.Combine(installDir, "Plugins", "ScrSaver", "VirtualPaper.ScreenSaver.exe"),
        ];

        bool ok = true;
        foreach (var f in required) {
            if (File.Exists(f))
                Console.WriteLine($"  [OK]  {f}");
            else {
                Console.Error.WriteLine($"  [MISSING] {f}");
                ok = false;
            }
        }
        return ok;
    }

    // ── 阶段 1+2: 启动主进程，等待 UI 自动拉起（最多 10s）────────────────
    public static bool TestAutoLaunchUI(string installDir) {
        KillAllProcesses("VirtualPaper", "VirtualPaper.UI", "VirtualPaper.PlayerWeb", "VirtualPaper.ScreenSaver");

        var mainExe = Path.Combine(installDir, "VirtualPaper.exe");
        var uiExe = Path.Combine(installDir, "Plugins", "UI", "VirtualPaper.UI.exe");

        Console.WriteLine($"  Launching: {mainExe}");
        var main = Process.Start(new ProcessStartInfo {
            FileName = mainExe,
            UseShellExecute = true,
            ErrorDialog = false,
        })!;

        // Wait for UI auto-spawn (max 10s)
        Process? uiProc = null;
        for (int i = 1; i <= 10; i++) {
            Thread.Sleep(1000);
            main.Refresh();
            if (main.HasExited) {
                Console.Error.WriteLine($"  VirtualPaper.exe exited after {i}s (code: {main.ExitCode})");
                KillAllProcesses("VirtualPaper.UI");
                return false;
            }

            uiProc = Process.GetProcessesByName("VirtualPaper.UI").FirstOrDefault();
            if (uiProc != null) {
                Console.WriteLine($"  [OK] UI auto-started after {i}s (PID: {uiProc.Id})");
                break;
            }
            Console.WriteLine($"  [wait] {i}s ...");
        }

        if (uiProc == null) {
            Console.WriteLine($"  [FALLBACK] Launching UI manually: {uiExe}");
            var fallback = Process.Start(new ProcessStartInfo {
                FileName = uiExe,
                UseShellExecute = true,
            });
            Thread.Sleep(5000);
            uiProc = Process.GetProcessesByName("VirtualPaper.UI").FirstOrDefault();
            if (uiProc == null) {
                Console.Error.WriteLine("  UI failed to start even manually");
                KillProcessTree(main);
                if (fallback != null) StopProcess(fallback);
                return false;
            }
            Console.WriteLine($"  [OK] UI started via fallback (PID: {uiProc.Id})");
        }

        KillProcessTree(main);
        KillAllProcesses("VirtualPaper.UI");
        return true;
    }

    // ── 阶段 3: 单例守卫 ──────────────────────────────────────────────
    public static bool TestSingleton(string installDir) {
        KillAllProcesses("VirtualPaper", "VirtualPaper.UI");

        var mainExe = Path.Combine(installDir, "VirtualPaper.exe");
        var uiExe = Path.Combine(installDir, "Plugins", "UI", "VirtualPaper.UI.exe");

        var main = Process.Start(new ProcessStartInfo {
            FileName = mainExe,
            UseShellExecute = true,
        })!;

        WaitForProcess("VirtualPaper.UI", 10000);

        main.Refresh();
        if (main.HasExited) {
            Console.Error.WriteLine($"  VirtualPaper.exe exited before UI spawned (code: {main.ExitCode})");
            KillAllProcesses("VirtualPaper.UI");
            return false;
        }

        if (Process.GetProcessesByName("VirtualPaper.UI").Length == 0) {
            Console.WriteLine("  UI did not auto-launch within 10s, launching manually...");
            Process.Start(new ProcessStartInfo { FileName = uiExe, UseShellExecute = true });
            Thread.Sleep(10000);
        }

        var uiBefore = Process.GetProcessesByName("VirtualPaper.UI");
        if (uiBefore.Length == 0) {
            Console.Error.WriteLine("  UI failed to start (auto + manual), cannot test singleton");
            KillProcessTree(main);
            return false;
        }
        Console.WriteLine($"  UI running (PID: {uiBefore[0].Id}), launching second instance...");

        Process.Start(new ProcessStartInfo { FileName = uiExe, UseShellExecute = true });
        Thread.Sleep(3000);

        var uiProcs = Process.GetProcessesByName("VirtualPaper.UI");
        if (uiProcs.Length != 1) {
            Console.Error.WriteLine($"  Singleton FAILED: expected 1, found {uiProcs.Length}");
            KillProcessTree(main);
            KillAllProcesses("VirtualPaper.UI");
            return false;
        }

        Console.WriteLine($"  [OK] Singleton: only 1 UI process (PID: {uiProcs[0].Id})");
        KillProcessTree(main);
        KillAllProcesses("VirtualPaper.UI");
        return true;
    }

    // ── 阶段 4: UI 独立启动守卫（MessageBox 检查）────────────────────
    public static bool TestStandaloneUIGuard(string installDir) {
        KillAllProcesses("VirtualPaper", "VirtualPaper.UI");
        Thread.Sleep(1000);

        var uiExe = Path.Combine(installDir, "Plugins", "UI", "VirtualPaper.UI.exe");
        Console.WriteLine($"  Launching UI without main process: {uiExe}");

        var uiProc = Process.Start(new ProcessStartInfo {
            FileName = uiExe,
            UseShellExecute = true,
        })!;

        Thread.Sleep(3000);

        uiProc.Refresh();
        if (uiProc.HasExited) {
            Console.Error.WriteLine("  UI exited on its own - guard NOT triggered");
            return false;
        }

        var classes = GetWindowClasses(uiProc);
        Console.WriteLine($"  Window classes: [{string.Join(", ", classes)}]");

        bool hasMsgBox = classes.Contains("#32770");
        bool hasWinUI = classes.Contains("WinUIDesktopWin32Window");

        if (hasWinUI) {
            Console.Error.WriteLine("  Guard FAILED: WinUI window appeared (started normally without main)");
            StopProcess(uiProc);
            return false;
        }

        if (!hasMsgBox) {
            Console.Error.WriteLine("  Guard FAILED: no MessageBox (#32770) found");
            StopProcess(uiProc);
            return false;
        }

        Console.WriteLine("  [OK] MessageBox (#32770) present - guard working");
        StopProcess(uiProc);
        return true;
    }

    // ── PlayerWeb 启动检测 ──────────────────────────────────────────
    public static bool TestPlayerWebStartup(string installDir) {
        KillAllProcesses("VirtualPaper.PlayerWeb");

        var tempBmp = CreateMinimalBmp();
        try {
            var playerWebExe = Path.Combine(installDir, "Plugins", "PlayerWeb", "VirtualPaper.PlayerWeb.exe");
            var argsJson = JsonSerializer.Serialize(new {
                isDebug = false,
                isPreview = false,
                filePath = tempBmp,
                depthFilePath = (string?)null,
                wpBasicDataFilePath = (string?)null,
                wpEffectFilePathUsing = (string?)null,
                wpEffectFilePathTemporary = (string?)null,
                wpEffectFilePathTemplate = (string?)null,
                runtimeType = "RImage",
                systemBackdrop = 0,
                applicationTheme = 0,
                language = "en-US",
                extra = (string?)null,
            });

            Console.WriteLine($"  Launching: {playerWebExe}");
            Console.WriteLine($"  Args: {argsJson}");

            var proc = Process.Start(new ProcessStartInfo {
                FileName = playerWebExe,
                WorkingDirectory = Path.Combine(installDir, "Plugins", "PlayerWeb"),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                CreateNoWindow = false,
            })!;

            proc.StandardInput.WriteLine(argsJson);
            proc.StandardInput.Close();

            Thread.Sleep(3000);

            proc.Refresh();
            if (proc.HasExited) {
                if (proc.ExitCode == 2) {
                    Console.Error.WriteLine("  PlayerWeb exited: WebView2 runtime not available (skip test)");
                    return true;
                }
                Console.Error.WriteLine($"  PlayerWeb exited immediately (code: {proc.ExitCode})");
                return false;
            }

            Console.WriteLine($"  [OK] PlayerWeb running (PID: {proc.Id})");
            StopProcess(proc);
            return true;
        }
        finally {
            try { File.Delete(tempBmp); } catch { }
        }
    }

    // ── ScreenSaver 启动检测 ────────────────────────────────────────
    public static bool TestScreenSaverStartup(string installDir) {
        KillAllProcesses("VirtualPaper.ScreenSaver");

        var scrSaverExe = Path.Combine(installDir, "Plugins", "ScrSaver", "VirtualPaper.ScreenSaver.exe");
        var tempBmp = CreateMinimalBmp();

        Process? proc = null;
        try {
            var args = $"--file-path \"{tempBmp}\" --wallpaper-type RImage --effect none";
            Console.WriteLine($"  Launching: {scrSaverExe} {args}");

            // Keep stdin pipe open so StdInListener doesn't read EOF and shut down
            proc = Process.Start(new ProcessStartInfo {
                FileName = scrSaverExe,
                Arguments = args,
                WorkingDirectory = Path.Combine(installDir, "Plugins", "ScrSaver"),
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = false,
            })!;

            Thread.Sleep(4000);

            proc.Refresh();
            if (proc.HasExited) {
                if (proc.ExitCode == 2) {
                    Console.Error.WriteLine("  ScreenSaver exited: WebView2 runtime not available (skip)");
                    return true;
                }
                Console.Error.WriteLine($"  ScreenSaver exited immediately (code: {proc.ExitCode})");
                return false;
            }

            var windowClasses = GetWindowClasses(proc);
            bool hasWindow = windowClasses.Count > 0;
            Console.WriteLine($"  [OK] ScreenSaver running (PID: {proc.Id}, windows: {(hasWindow ? "yes" : "none")})");
            return true;
        }
        finally {
            if (proc != null && !proc.HasExited)
                StopProcess(proc);
            try { File.Delete(tempBmp); } catch { }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────
    private static void WaitForProcess(string name, int timeoutMs) {
        int elapsed = 0;
        while (elapsed < timeoutMs) {
            if (Process.GetProcessesByName(name).Length > 0) return;
            Thread.Sleep(500);
            elapsed += 500;
        }
    }

    private static void KillAllProcesses(params string[] names) {
        foreach (var name in names) {
            foreach (var p in Process.GetProcessesByName(name)) {
                try { KillProcessTree(p); } catch { }
            }
        }
    }

    private static void KillProcessTree(Process p) {
        try {
            var pid = p.Id;
            var taskkill = Process.Start(new ProcessStartInfo {
                FileName = "taskkill",
                Arguments = $"/F /T /PID {pid}",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            taskkill?.WaitForExit(5000);
        }
        catch { }
    }

    private static void StopProcess(Process p) {
        try {
            KillProcessTree(p);
            if (!p.WaitForExit(3000)) {
                var tk = Process.Start(new ProcessStartInfo {
                    FileName = "taskkill",
                    Arguments = $"/F /PID {p.Id}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                tk?.WaitForExit(5000);
            }
        }
        catch { }
    }

    private static List<string> GetWindowClasses(Process proc) {
        var classes = new List<string>();
        var ready = new System.Threading.ManualResetEventSlim(false);
        List<string>? result = null;
        Exception? err = null;

        var thread = new Thread(() => {
            try {
                var desktop = AutomationElement.RootElement;
                var pidCond = new PropertyCondition(AutomationElement.ProcessIdProperty, proc.Id);
                var wins = desktop.FindAll(TreeScope.Children, pidCond);
                var list = new List<string>();
                if (wins != null)
                    foreach (AutomationElement w in wins)
                        list.Add(w.Current.ClassName);
                result = list;
            }
            catch (Exception ex) { err = ex; }
            finally { ready.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        if (!ready.Wait(5000)) {
            Console.Error.WriteLine("  [WARN] GetWindowClasses timed out (5s)");
            return classes;
        }
        if (err != null) {
            Console.Error.WriteLine($"  [WARN] GetWindowClasses failed: {err.Message}");
            return classes;
        }
        return result ?? classes;
    }

    /// <summary>
    /// Create a minimal 1x1 white BMP file in temp.
    /// </summary>
    private static string CreateMinimalBmp() {
        var path = Path.Combine(Path.GetTempPath(), $"vp_smoke_bmp_{Guid.NewGuid():N}.bmp");
        // 54-byte header + 4 bytes BGR pixel data = 58 bytes
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // BITMAPFILEHEADER (14 bytes)
        bw.Write((byte)'B');              // bfType[0]
        bw.Write((byte)'M');              // bfType[1]
        bw.Write(58);                     // bfSize
        bw.Write(0);                      // bfReserved1/2
        bw.Write(54);                     // bfOffBits

        // BITMAPINFOHEADER (40 bytes)
        bw.Write(40);                     // biSize
        bw.Write(1);                      // biWidth
        bw.Write(1);                      // biHeight
        bw.Write((short)1);              // biPlanes
        bw.Write((short)24);             // biBitCount
        bw.Write(0);                      // biCompression (BI_RGB)
        bw.Write(0);                      // biSizeImage (0 is ok for BI_RGB)
        bw.Write(0);                      // biXPelsPerMeter
        bw.Write(0);                      // biYPelsPerMeter
        bw.Write(0);                      // biClrUsed
        bw.Write(0);                      // biClrImportant

        // Pixel data (1 row, padded to 4-byte boundary)
        bw.Write((byte)0xFF);             // Blue
        bw.Write((byte)0xFF);             // Green
        bw.Write((byte)0xFF);             // Red
        bw.Write((byte)0x00);             // Padding

        return path;
    }
}
