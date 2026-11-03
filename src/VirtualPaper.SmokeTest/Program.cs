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

var started = DateTimeOffset.UtcNow;
var stopwatch = Stopwatch.StartNew();
string dataRoot = Path.Combine(options.ResultsFile != null
    ? Path.GetDirectoryName(options.ResultsFile)!
    : Path.Combine(Path.GetTempPath(), $"VirtualPaper-smoke-{Guid.NewGuid():N}"), "app-data");
SmokeCheckResult result;
try {
    AppStartSmoke.TestLaunchUI(options.InstallDir, options.Timeout, dataRoot);
    result = new("Launch", true, stopwatch.ElapsedMilliseconds);
}
catch (Exception ex) {
    Console.Error.WriteLine(ex);
    result = new("Launch", false, stopwatch.ElapsedMilliseconds, ex.Message);
}
if (options.ResultsFile != null) {
    Directory.CreateDirectory(Path.GetDirectoryName(options.ResultsFile)!);
    var report = new SmokeReport(1, "Launch", started, stopwatch.ElapsedMilliseconds,
        1, result.Passed ? 1 : 0, result.Passed ? 0 : 1, [result]);
    File.WriteAllText(options.ResultsFile, JsonSerializer.Serialize(report,
        new JsonSerializerOptions { WriteIndented = true }));
}
return result.Passed ? 0 : 1;
