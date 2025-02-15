using VirtualPaper.Common.Utils.Drafts.Models;

namespace VirtualPaper.Common.Utils.Drafts {
    public class VpdParser {
        public static async Task<DesignDocument> ParseAsync(string filePath) {
            var designDoc = new DesignDocument();
            var lines = await File.ReadAllLinesAsync(filePath);

            bool inDesignSection = false;
            bool inProjectsSection = false;

            foreach (var line in lines) {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']')) {
                    switch (trimmedLine.ToLower()) {
                        case "[Design]":
                            inDesignSection = true;
                            inProjectsSection = false;
                            break;
                        case $"{nameof(designDoc.Projects)}":
                            inDesignSection = false;
                            inProjectsSection = true;
                            break;
                        default:
                            inDesignSection = inProjectsSection = false;
                            break;
                    }
                    continue;
                }

                if (inDesignSection && trimmedLine.Length > 0 && !trimmedLine.StartsWith('#')) {
                    var parts = trimmedLine.Split('=');
                    switch (parts[0]) {
                        case nameof(designDoc.Name):
                            designDoc.Name = parts[1];
                            break;
                        case nameof(designDoc.Version):
                            designDoc.Version = parts[1];
                            break;
                        case nameof(designDoc.CreatedOn):
                            designDoc.CreatedOn = DateTime.Parse(parts[1]);
                            break;
                    }
                }
                else if (inProjectsSection && trimmedLine.Length > 0 && !trimmedLine.StartsWith('#')) {
                    var parts = trimmedLine.Split('|');
                    var project = new DesignProject {
                        Name = parts[0],
                        Type = (ProjectType)int.Parse(parts[1]),
                        Path = parts[2],
                        Guid = Guid.Parse(parts[4])
                    };
                    designDoc.Projects.Add(project);
                }
            }

            return designDoc;
        }
    }
}
