using VirtualPaper.SmokeTest;

var installDir = args switch {
    ["--install-dir", var dir, ..] => dir,
    _ => null,
};

if (string.IsNullOrEmpty(installDir)) {
    Console.Error.WriteLine("Usage: dotnet run -- --install-dir <path>");
    return 1;
}

Console.WriteLine($"Smoke Test | Install Dir: {installDir}");
Console.WriteLine(new string('=', 60));

var results = new List<(string Name, bool Passed)>();

// ── App Startup ────────────────────────────────────────────────
results.Add(Run("Verify key files", () => AppStartSmoke.VerifyKeyFiles(installDir)));
results.Add(Run("Auto-launch UI", () => AppStartSmoke.TestAutoLaunchUI(installDir)));
Thread.Sleep(1000);
results.Add(Run("Singleton guard", () => AppStartSmoke.TestSingleton(installDir)));
Thread.Sleep(1000);
results.Add(Run("Standalone UI guard", () => AppStartSmoke.TestStandaloneUIGuard(installDir)));
Thread.Sleep(1000);
results.Add(Run("PlayerWeb startup", () => AppStartSmoke.TestPlayerWebStartup(installDir)));
Thread.Sleep(1000);
results.Add(Run("ScreenSaver startup", () => AppStartSmoke.TestScreenSaverStartup(installDir)));
Thread.Sleep(1000);

// ── Download ───────────────────────────────────────────────────
results.Add(Run("Single download", () => DownloadSmoke.TestSingleDownload()));
results.Add(Run("Cancel mid-download", () => DownloadSmoke.TestCancelDownload()));
results.Add(Run("Multi-file download", () => DownloadSmoke.TestMultiDownload()));

// ── Summary ────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine(new string('=', 60));
Console.WriteLine("RESULTS:");

int failed = 0;
foreach (var (name, passed) in results) {
    Console.WriteLine(passed
        ? $"  [PASS] {name}"
        : $"  [FAIL] {name}");
    if (!passed) failed++;
}

Console.WriteLine();
Console.WriteLine($"Total: {results.Count} | Passed: {results.Count - failed} | Failed: {failed}");

return failed > 0 ? 1 : 0;

// ── Helper ─────────────────────────────────────────────────────
static (string, bool) Run(string name, Func<bool> test, int timeoutMs = 60000) {
    Console.WriteLine();
    Console.WriteLine($"--- {name} ---");
    try {
        var task = Task.Run(test);
        if (!task.Wait(timeoutMs)) {
            Console.Error.WriteLine($"  [TIMEOUT] {name} exceeded {timeoutMs}ms");
            return (name, false);
        }
        var passed = task.Result;
        Console.WriteLine(passed ? "  [PASS]" : "  [FAIL]");
        return (name, passed);
    }
    catch (AggregateException ae) when (ae.InnerException is TimeoutException) {
        Console.Error.WriteLine($"  [TIMEOUT] {ae.InnerException.Message}");
        return (name, false);
    }
    catch (Exception ex) {
        var inner = ex is AggregateException ae2 ? ae2.InnerException ?? ex : ex;
        Console.Error.WriteLine($"  [ERROR] {inner.Message}");
        return (name, false);
    }
}
