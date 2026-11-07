using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using VirtualPaper.Common.Utils.Files;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    public sealed record WebProjectTextSnapshot(string FilePath, string Content, string EncodingText);

    /// <summary>Copies a complete project without saving changes into the source project.</summary>
    public static class WebProjectCopy {
        public static string Copy(WebDesignFileUtil project, string destinationFolder,
            IReadOnlyList<WebProjectTextSnapshot> edits, CancellationToken token = default) {
            string source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project.ProjectFolder));
            string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationFolder));
            if (source.Equals(destination, StringComparison.OrdinalIgnoreCase)
                || FileUtil.IsPathWithinDirectory(source, destination)
                || FileUtil.IsPathWithinDirectory(destination, source)) {
                throw new IOException("Choose an empty folder outside the current project.");
            }
            string parent = Path.GetDirectoryName(destination)
                ?? throw new IOException("A drive root cannot be used as a project folder.");
            FileUtil.EnsureNoDirectoryLinks(source);
            FileUtil.EnsureNoDirectoryLinks(destination);
            EnsureEmptyDestination(destination);
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(parent);
            string staging = Path.Combine(parent, ".vp-copy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            bool destinationRemoved = false;
            try {
                FileUtil.CopyDirectory(source, staging, copySubDirs: true, overwrite: false,
                    cancellationToken: token, rejectLinks: true, clearReadOnly: true);
                foreach (var edit in edits) {
                    token.ThrowIfCancellationRequested();
                    string relative = FileUtil.GetContainedRelativePath(source, edit.FilePath, nameof(edits));
                    string target = Path.Combine(staging, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllText(target, edit.Content, WebEditorFile.GetEncodingFromText(edit.EncodingText));
                }

                string oldProjectName = Path.GetFileName(project.ProjectFilePath);
                string newName = Path.GetFileName(destination);
                string newProjectName = newName + ".vpw";
                string stagedProject = Path.Combine(staging, oldProjectName);
                var manifest = JsonNode.Parse(File.ReadAllText(stagedProject)) as JsonObject
                    ?? throw new InvalidDataException("The project manifest must be a JSON object.");
                if (manifest["files"] is not JsonArray files) {
                    throw new InvalidDataException("The project manifest has no file list.");
                }
                bool hasSolution = false;
                foreach (var item in files) {
                    if (item is not JsonObject file || file["path"]?.GetValue<string>() is not string path) {
                        throw new InvalidDataException("The project manifest contains an invalid file entry.");
                    }
                    FileUtil.GetContainedRelativePath(staging, Path.Combine(staging, path), nameof(project));
                    if (file["role"]?.GetValue<string>() == "solution"
                        || path.Equals(oldProjectName, StringComparison.OrdinalIgnoreCase)) {
                        file["path"] = newProjectName;
                        file["role"] = "solution";
                        hasSolution = true;
                    }
                }
                if (!hasSolution) {
                    files.Add(new JsonObject { ["path"] = newProjectName, ["type"] = "vpw", ["role"] = "solution" });
                }
                manifest["name"] = newName;
                string newProject = Path.Combine(staging, newProjectName);
                if (!oldProjectName.Equals(newProjectName, StringComparison.OrdinalIgnoreCase)) {
                    // A differently named project already in the source is user data, not ours to overwrite.
                    if (File.Exists(newProject)) throw new IOException("The copied project name conflicts with an existing file.");
                    File.Delete(stagedProject);
                }
                File.WriteAllText(newProject, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

                token.ThrowIfCancellationRequested();
                EnsureEmptyDestination(destination);
                if (Directory.Exists(destination)) {
                    Directory.Delete(destination, recursive: false);
                    destinationRemoved = true;
                }
                Directory.Move(staging, destination);
                return Path.Combine(destination, newProjectName);
            }
            catch {
                if (destinationRemoved && !Directory.Exists(destination) && !File.Exists(destination)) {
                    Directory.CreateDirectory(destination);
                }
                throw;
            }
            finally {
                if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            }
        }

        private static void EnsureEmptyDestination(string path) {
            if (File.Exists(path) || (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())) {
                throw new IOException("The destination project folder must be empty. Existing files will not be overwritten.");
            }
        }
    }
}
