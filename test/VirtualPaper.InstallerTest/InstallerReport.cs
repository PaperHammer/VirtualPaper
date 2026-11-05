namespace VirtualPaper.InstallerTest;

internal sealed class ApplicationResult {
    public int CoreProcessId { get; set; }
    public int UiProcessId { get; set; }
    public bool OpenedNormally { get; set; }
    public bool ExitedNormally { get; set; }
    public bool ForcedCleanup { get; set; }
    public string? ExpectedLanguage { get; set; }
    public string? ActualLanguage { get; set; }
    public string? PersistedLanguage { get; set; }
    public bool LanguageVerified { get; set; }
    public long DurationMilliseconds { get; set; }
    public string? Error { get; set; }
    public bool Passed => OpenedNormally && ExitedNormally && LanguageVerified && !ForcedCleanup && Error is null;
}
