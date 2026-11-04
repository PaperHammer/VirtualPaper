namespace VirtualPaper.InstallerTest;

internal sealed class ApplicationResult {
    public int CoreProcessId { get; set; }
    public int UiProcessId { get; set; }
    public bool OpenedNormally { get; set; }
    public bool ExitedNormally { get; set; }
    public bool ForcedCleanup { get; set; }
    public long DurationMilliseconds { get; set; }
    public string? Error { get; set; }
    public bool Passed => OpenedNormally && ExitedNormally && !ForcedCleanup && Error is null;
}
