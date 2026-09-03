using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VirtualPaper.SmokeTest;

if (!SmokeOptions.TryParse(args, out var options, out var error)) {
    Console.Error.WriteLine(error);
    SmokeOptions.PrintUsage(Console.Error);
    return 2;
}
if (options.ShowHelp) {
    SmokeOptions.PrintUsage(Console.Out);
    return 0;
}

Console.WriteLine($"Smoke Test | Suite: {options.Suite}");
if (options.InstallDir != null)
    Console.WriteLine($"Install Dir: {options.InstallDir}");
Console.WriteLine(new string('=', 60));

var startedAtUtc = DateTimeOffset.UtcNow;
var suiteStopwatch = Stopwatch.StartNew();
var results = new List<SmokeCheckResult>();

// ── App Startup ────────────────────────────────────────────────
if (options.Suite is SmokeSuite.All or SmokeSuite.Startup) {
    string installDir = options.InstallDir!;
    bool filesPresent = AddResult("Verify key files", () => AppStartSmoke.VerifyKeyFiles(installDir));
    if (filesPresent) {
        AddResult("Auto-launch UI", () => AppStartSmoke.TestAutoLaunchUI(installDir));
        AddResult("Singleton guard", () => AppStartSmoke.TestSingleton(installDir));
        AddResult("Standalone UI guard", () => AppStartSmoke.TestStandaloneUIGuard(installDir));
        AddResult("PlayerWeb startup", () => AppStartSmoke.TestPlayerWebStartup(installDir));
        AddResult("ScreenSaver startup", () => AppStartSmoke.TestScreenSaverStartup(installDir));
        AddResult("Process cleanup", AppStartSmoke.TestProcessCleanup);
    }
    else {
        Console.Error.WriteLine("Skipping process startup checks because required files are missing.");
    }
}

// ── Download ───────────────────────────────────────────────────
if (options.Suite is SmokeSuite.All or SmokeSuite.Download) {
    AddResult("Single download", DownloadSmoke.TestSingleDownload);
    AddResult("Cancel mid-download", DownloadSmoke.TestCancelDownload);
    AddResult("Multi-file download", DownloadSmoke.TestMultiDownload);
}

// ── Summary ────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine(new string('=', 60));
Console.WriteLine("RESULTS:");

int failed = 0;
foreach (var result in results) {
    Console.WriteLine(result.Passed
        ? $"  [PASS] {result.Name} ({result.DurationMilliseconds}ms)"
        : $"  [FAIL] {result.Name} ({result.DurationMilliseconds}ms): {result.Error}");
    if (!result.Passed) failed++;
}

Console.WriteLine();
Console.WriteLine($"Total: {results.Count} | Passed: {results.Count - failed} | Failed: {failed}");

suiteStopwatch.Stop();
if (options.ResultsFile != null) {
    try {
        var report = new SmokeReport(
            SchemaVersion: 1,
            Suite: options.Suite.ToString(),
            StartedAtUtc: startedAtUtc,
            DurationMilliseconds: suiteStopwatch.ElapsedMilliseconds,
            Total: results.Count,
            Passed: results.Count - failed,
            Failed: failed,
            Checks: results);
        var directory = Path.GetDirectoryName(options.ResultsFile);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(
            options.ResultsFile,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Results: {options.ResultsFile}");
    }
    catch (Exception ex) {
        Console.Error.WriteLine($"Failed to write smoke results: {ex.Message}");
        failed++;
    }
}

return failed > 0 ? 1 : 0;

// ── Helper ─────────────────────────────────────────────────────
bool AddResult(string name, Func<bool> test) {
    var result = Run(name, test, options.Timeout);
    results.Add(result);
    return result.Passed;
}

static SmokeCheckResult Run(string name, Func<bool> test, TimeSpan timeout) {
    Console.WriteLine();
    Console.WriteLine($"--- {name} ---");
    var stopwatch = Stopwatch.StartNew();
    try {
        var task = Task.Run(test);
        if (!task.Wait(timeout)) {
            var error = $"Exceeded {timeout.TotalSeconds:0.###}s";
            Console.Error.WriteLine($"  [TIMEOUT] {name} {error}");
            return new(name, false, stopwatch.ElapsedMilliseconds, error);
        }
        var passed = task.Result;
        Console.WriteLine(passed
            ? $"  [PASS] ({stopwatch.ElapsedMilliseconds}ms)"
            : $"  [FAIL] ({stopwatch.ElapsedMilliseconds}ms)");
        return new(name, passed, stopwatch.ElapsedMilliseconds, passed ? null : "Check returned false");
    }
    catch (AggregateException ae) when (ae.InnerException is TimeoutException) {
        Console.Error.WriteLine($"  [TIMEOUT] {ae.InnerException.Message}");
        return new(name, false, stopwatch.ElapsedMilliseconds, ae.InnerException.Message);
    }
    catch (Exception ex) {
        var inner = ex is AggregateException ae2 ? ae2.InnerException ?? ex : ex;
        Console.Error.WriteLine($"  [ERROR] {inner.Message}");
        return new(name, false, stopwatch.ElapsedMilliseconds, inner.Message);
    }
}
