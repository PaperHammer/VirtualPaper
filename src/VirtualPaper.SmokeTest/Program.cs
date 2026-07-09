using VirtualPaper.SmokeTest;

var installDir = args switch
{
    ["--install-dir", var dir, ..] => dir,
    _ => null,
};

if (string.IsNullOrEmpty(installDir))
{
    Console.Error.WriteLine("Usage: dotnet run -- --install-dir <path>");
    return 1;
}

Console.WriteLine($"Smoke Test | Install Dir: {installDir}");
Console.WriteLine(new string('=', 60));

var results = new List<(string Name, bool Passed)>();

// ── App Startup ────────────────────────────────────────────────
results.Add(Run("Verify key files",     () => AppStartSmoke.VerifyKeyFiles(installDir)));
results.Add(Run("Auto-launch UI",       () => AppStartSmoke.TestAutoLaunchUI(installDir)));
results.Add(Run("Singleton guard",      () => AppStartSmoke.TestSingleton(installDir)));
results.Add(Run("Standalone UI guard",  () => AppStartSmoke.TestStandaloneUIGuard(installDir)));
results.Add(Run("PlayerWeb startup",    () => AppStartSmoke.TestPlayerWebStartup(installDir)));
results.Add(Run("ScreenSaver startup",  () => AppStartSmoke.TestScreenSaverStartup(installDir)));

// ── Download ───────────────────────────────────────────────────
results.Add(Run("Single download",      () => DownloadSmoke.TestSingleDownload()));
results.Add(Run("Cancel mid-download",  () => DownloadSmoke.TestCancelDownload()));
results.Add(Run("Multi-file download",  () => DownloadSmoke.TestMultiDownload()));

// ── Summary ────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine(new string('=', 60));
Console.WriteLine("RESULTS:");

int failed = 0;
foreach (var (name, passed) in results)
{
    Console.WriteLine(passed
        ? $"  [PASS] {name}"
        : $"  [FAIL] {name}");
    if (!passed) failed++;
}

Console.WriteLine();
Console.WriteLine($"Total: {results.Count} | Passed: {results.Count - failed} | Failed: {failed}");

return failed > 0 ? 1 : 0;

// ── Helper ─────────────────────────────────────────────────────
static (string, bool) Run(string name, Func<bool> test)
{
    Console.WriteLine();
    Console.WriteLine($"--- {name} ---");
    try
    {
        var passed = test();
        Console.WriteLine(passed ? "  [PASS]" : "  [FAIL]");
        return (name, passed);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  [ERROR] {ex.Message}");
        return (name, false);
    }
}
