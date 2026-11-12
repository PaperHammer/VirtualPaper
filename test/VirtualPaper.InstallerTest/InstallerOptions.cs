using System.IO;

namespace VirtualPaper.InstallerTest;

internal sealed record InstallerOptions(string InstallDir, string? ResultsFile, TimeSpan Timeout, bool ShowHelp,
    string? WizardExecutable = null, bool Uninstall = false, string Language = "english",
    string? DataRoot = null, string ProfileMode = "first-run") {
    public static bool TryParse(string[] args, out InstallerOptions options, out string? error) {
        options = new("", null, TimeSpan.FromSeconds(60), false);
        error = null;
        for (int i = 0; i < args.Length; i++) {
            if (args[i] is "--help" or "-h") {
                options = options with { ShowHelp = true };
                continue;
            }
            string arg = args[i];
            if (++i >= args.Length || args[i].StartsWith('-')) {
                error = $"Missing value for '{arg}'.";
                return false;
            }
            switch (arg) {
                case "--wizard-install" or "--wizard-uninstall" when options.WizardExecutable is null:
                    options = options with { WizardExecutable = Path.GetFullPath(args[i]), Uninstall = arg == "--wizard-uninstall" };
                    break;
                case "--install-dir": options = options with { InstallDir = Path.GetFullPath(args[i]) }; break;
                case "--results-file": options = options with { ResultsFile = Path.GetFullPath(args[i]) }; break;
                case "--data-root": options = options with { DataRoot = Path.GetFullPath(args[i]) }; break;
                case "--profile-mode" when args[i] is "first-run" or "prepare" or "preserve":
                    options = options with { ProfileMode = args[i] };
                    break;
                case "--language" when args[i] is "english" or "chinese" or "french":
                    options = options with { Language = args[i] };
                    break;
                case "--timeout-seconds" when int.TryParse(args[i], out int seconds) && seconds is >= 1 and <= 600:
                    options = options with { Timeout = TimeSpan.FromSeconds(seconds) };
                    break;
                default:
                    error = $"Unknown argument or invalid value: {arg} {args[i]}";
                    return false;
            }
        }
        if (!options.ShowHelp && options.WizardExecutable != null &&
            (!File.Exists(options.WizardExecutable) || options.ResultsFile is null || string.IsNullOrEmpty(options.InstallDir))) {
            error = "Wizard mode requires an existing executable, --install-dir and --results-file.";
            return false;
        }
        if (!options.ShowHelp && options.WizardExecutable is null && !Directory.Exists(options.InstallDir)) {
            error = "--install-dir must point to an existing installation.";
            return false;
        }
        return true;
    }

    public static void PrintUsage(TextWriter writer) => writer.WriteLine(
        "Usage: dotnet run -- --install-dir <path> [--timeout-seconds 60] [--results-file <path>] [--data-root <path>] [--profile-mode first-run|prepare|preserve]\n" +
        "Wizard: --wizard-install <setup.exe> | --wizard-uninstall <unins.exe>, plus --install-dir, --results-file and optional --language english|chinese|french.\n" +
        "For install -> open -> uninstall, use test/Run-InstallerTests.ps1.");
}
