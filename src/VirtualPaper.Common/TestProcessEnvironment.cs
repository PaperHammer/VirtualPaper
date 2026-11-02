namespace VirtualPaper.Common;

/// <summary>Opt-in, inherited process isolation for executable integration tests.</summary>
public static class TestProcessEnvironment {
    public const string SessionVariable = "VIRTUALPAPER_TEST_SESSION";
    public const string DataRootVariable = "VIRTUALPAPER_TEST_DATA_ROOT";
    public static string? SessionId { get; }
    public static string? DataRoot { get; }
    public static bool IsEnabled => SessionId != null;

    public static void ConfigureLogging(string module) {
        if (!IsEnabled) return;
        if (NLog.LogManager.Configuration?.FindTargetByName<NLog.Targets.FileTarget>("logfile") is { } target) {
            target.FileName = Path.Combine(DataRoot!, "logs", module, "${date:format=yyyyMMdd_HHmmss}.txt");
            NLog.LogManager.ReconfigExistingLoggers();
        }
    }

    static TestProcessEnvironment() {
        string? session = Environment.GetEnvironmentVariable(SessionVariable);
        string? root = Environment.GetEnvironmentVariable(DataRootVariable);
        if (string.IsNullOrEmpty(session) && string.IsNullOrEmpty(root)) return;
        if (!Guid.TryParseExact(session, "N", out _) || string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException("Process tests require a session GUID and an absolute data root.");
        root = Path.GetFullPath(root);
        if (root.TrimEnd(Path.DirectorySeparatorChar) == Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidOperationException("A drive root cannot be used as a test data directory.");
        SessionId = session;
        DataRoot = root;
    }
}
