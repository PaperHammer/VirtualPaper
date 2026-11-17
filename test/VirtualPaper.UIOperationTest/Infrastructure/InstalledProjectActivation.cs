using System.Diagnostics;
using Microsoft.Win32;
using VirtualPaper.TestSupport;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class InstalledProjectActivation {
    public static bool IsEnabled => Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_INSTALLED_EXE") == "1";

    public static Process Start(string path) {
        string core = Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_CORE_EXE")!;
        string extension = Path.GetExtension(path).ToLowerInvariant();
        string type = extension switch { ".vpd" => "Design", ".vpw" => "Web", _ => throw new ArgumentException("Not a project file.") };
        string progId = "VirtualPaper." + type;
        string root = Path.GetDirectoryName(core)!;
        using var command = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command");
        Check(string.Equals(command?.GetValue(null) as string, $"\"{core}\" --open-project \"%1\"", StringComparison.OrdinalIgnoreCase),
            "The installed project command does not target this test installation.");
        using var icon = Registry.ClassesRoot.OpenSubKey(progId + @"\DefaultIcon");
        string iconPath = Path.Combine(root, "FileIcons", extension.TrimStart('.') + ".ico");
        Check(string.Equals(icon?.GetValue(null) as string, $"\"{iconPath}\"", StringComparison.OrdinalIgnoreCase) && File.Exists(iconPath),
            "The installed project icon is missing or points outside this test installation.");
        using var verb = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open");
        string locale = File.ReadAllText(Path.Combine(root, "installer-language.txt")).Trim();
        string expected = LocalizedTestStrings.Read(AppContext.BaseDirectory, locale, "Shell_OpenWithVirtualPaper");
        Check(verb?.GetValue(null) as string == expected, "The installed Open with VirtualPaper label is not localized.");
        using var association = Registry.ClassesRoot.OpenSubKey(extension);
        using var choice = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + extension + @"\UserChoice");
        string? selected = choice?.GetValue("ProgId") as string ?? association?.GetValue(null) as string;
        Check(selected == progId, "Another application owns the project default; acceptance will not override the user's choice.");
        // Use the actual Windows file association, including its registered parameters.
        return Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "open" })
            ?? throw new InvalidOperationException("Windows did not return the file-activation process.");
    }
}
