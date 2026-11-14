namespace VirtualPaper.Common.Utils.Files;

/// <summary>The shared Explorer/command-line contract for opening saved projects.</summary>
public static class ProjectFileActivation {
    public const string OpenArgument = "--open-project";

    public static string[] ReadArguments(string[] arguments) {
        if (arguments.Length == 0) return [];
        // Other launch modes (including isolated package acceptance) keep their own arguments.
        if (arguments[0] != OpenArgument) {
            return IsProjectFile(arguments[0]) ? NormalizePaths(arguments) : [];
        }
        if (arguments.Length == 1) throw new ArgumentException("A project file is required.");
        return NormalizePaths(arguments.Skip(1));
    }

    public static string[] NormalizePaths(IEnumerable<string> paths) => paths.Select(path => {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !IsProjectFile(path))
            throw new ArgumentException("Only absolute .vpd and .vpw project paths are supported.");
        return Path.GetFullPath(path);
    }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static bool IsProjectFile(string path) =>
        Path.GetExtension(path).Equals(FileExtension.FE_Design, StringComparison.OrdinalIgnoreCase)
        || Path.GetExtension(path).Equals(FileExtension.FE_WebDesign, StringComparison.OrdinalIgnoreCase);
}

public sealed record ProjectOpenRequest(string Id, string[] Paths);
