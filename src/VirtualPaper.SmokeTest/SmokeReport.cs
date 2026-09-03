namespace VirtualPaper.SmokeTest;

internal sealed record SmokeCheckResult(
    string Name,
    bool Passed,
    long DurationMilliseconds,
    string? Error = null);

internal sealed record SmokeReport(
    int SchemaVersion,
    string Suite,
    DateTimeOffset StartedAtUtc,
    long DurationMilliseconds,
    int Total,
    int Passed,
    int Failed,
    IReadOnlyList<SmokeCheckResult> Checks);
