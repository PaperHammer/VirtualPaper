using System.IO;

namespace VirtualPaper.SmokeTest;

internal enum SmokeSuite {
    All,
    Startup,
    Download,
}

internal sealed record SmokeOptions(
    SmokeSuite Suite,
    string? InstallDir,
    string? ResultsFile,
    TimeSpan Timeout,
    bool ShowHelp) {
    public static bool TryParse(
        string[] args,
        out SmokeOptions options,
        out string? error) {
        SmokeSuite suite = SmokeSuite.All;
        string? installDir = null;
        string? resultsFile = null;
        int timeoutSeconds = 60;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++) {
            string arg = args[i];
            if (arg is "--help" or "-h") {
                showHelp = true;
                continue;
            }
            if (!TryReadValue(args, ref i, out string? value)) {
                options = Default;
                error = $"Missing value for '{arg}'.";
                return false;
            }

            switch (arg) {
                case "--install-dir":
                    installDir = value;
                    break;
                case "--results-file":
                    resultsFile = Path.GetFullPath(value!);
                    break;
                case "--suite" when Enum.TryParse(value, ignoreCase: true, out SmokeSuite parsed):
                    suite = parsed;
                    break;
                case "--suite":
                    options = Default;
                    error = $"Unknown suite '{value}'.";
                    return false;
                case "--timeout-seconds" when int.TryParse(value, out int parsed) && parsed > 0:
                    timeoutSeconds = parsed;
                    break;
                case "--timeout-seconds":
                    options = Default;
                    error = "Timeout must be a positive integer.";
                    return false;
                default:
                    options = Default;
                    error = $"Unknown argument '{arg}'.";
                    return false;
            }
        }

        if (!showHelp && suite is SmokeSuite.All or SmokeSuite.Startup) {
            if (string.IsNullOrWhiteSpace(installDir)) {
                options = Default;
                error = "--install-dir is required for the startup and all suites.";
                return false;
            }
            installDir = Path.GetFullPath(installDir);
            if (!Directory.Exists(installDir)) {
                options = Default;
                error = $"Install directory does not exist: {installDir}";
                return false;
            }
        }

        options = new SmokeOptions(suite, installDir, resultsFile, TimeSpan.FromSeconds(timeoutSeconds), showHelp);
        error = null;
        return true;
    }

    public static void PrintUsage(TextWriter writer) {
        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run -- --suite download [--timeout-seconds 60] [--results-file <path>]");
        writer.WriteLine("  dotnet run -- --install-dir <path> [--suite startup|all] [--timeout-seconds 60] [--results-file <path>]");
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value) {
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-')) {
            value = null;
            return false;
        }
        value = args[++index];
        return true;
    }

    private static SmokeOptions Default { get; } =
        new(SmokeSuite.All, null, null, TimeSpan.FromSeconds(60), false);
}
