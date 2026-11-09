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
        // Shell package activation does not inherit the test host's environment.
        // Carry only the existing isolation contract over explicit test arguments,
        // then let Core's normal child processes inherit it as before.
        if (ReadActivationArguments(Environment.GetCommandLineArgs().Skip(1).ToArray()) is { } activation) {
            session = activation.Session;
            root = activation.Root;
            Environment.SetEnvironmentVariable(SessionVariable, session);
            Environment.SetEnvironmentVariable(DataRootVariable, root);
        }
        if (string.IsNullOrEmpty(session) && string.IsNullOrEmpty(root)) return;
        (SessionId, DataRoot) = Validate(session, root);
    }

    internal static (string Session, string Root)? ReadActivationArguments(string[] arguments) {
        const string sessionFlag = "--virtualpaper-test-session";
        const string rootFlag = "--virtualpaper-test-data-root";
        if (!arguments.Contains(sessionFlag) && !arguments.Contains(rootFlag)) return null;
        if (arguments.Length != 4 || arguments[0] != sessionFlag || arguments[2] != rootFlag)
            throw new InvalidOperationException("Package test activation requires both isolation arguments.");
        return Validate(arguments[1], arguments[3]);
    }

    private static (string Session, string Root) Validate(string? session, string? root) {
        if (!Guid.TryParseExact(session, "N", out _) || string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException("Process tests require a session GUID and an absolute data root.");
        root = Path.GetFullPath(root);
        if (root.TrimEnd(Path.DirectorySeparatorChar) == Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidOperationException("A drive root cannot be used as a test data directory.");
        return (session!, root);
    }
}
