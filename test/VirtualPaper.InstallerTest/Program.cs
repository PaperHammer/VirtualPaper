using System.IO;
using System.Text.Json;
using VirtualPaper.InstallerTest;

if (!InstallerOptions.TryParse(args, out var options, out var error)) {
    Console.Error.WriteLine(error);
    InstallerOptions.PrintUsage(Console.Error);
    return 2;
}
if (options.ShowHelp) {
    InstallerOptions.PrintUsage(Console.Out);
    return 0;
}
if (options.WizardExecutable is not null) {
    var wizard = InstallerWizard.Run(options);
    Directory.CreateDirectory(Path.GetDirectoryName(options.ResultsFile!)!);
    File.WriteAllText(options.ResultsFile!, JsonSerializer.Serialize(wizard, new JsonSerializerOptions { WriteIndented = true }));
    return wizard.Passed ? 0 : 1;
}
string dataRoot = Path.Combine(options.ResultsFile != null
    ? Path.GetDirectoryName(options.ResultsFile)!
    : Path.Combine(Path.GetTempPath(), $"VirtualPaper-installer-{Guid.NewGuid():N}"), "app-data");
var report = ApplicationLifecycle.Run(options.InstallDir, options.Timeout, dataRoot);
if (options.ResultsFile != null) {
    Directory.CreateDirectory(Path.GetDirectoryName(options.ResultsFile)!);
    File.WriteAllText(options.ResultsFile, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
}
return report.Passed ? 0 : 1;
