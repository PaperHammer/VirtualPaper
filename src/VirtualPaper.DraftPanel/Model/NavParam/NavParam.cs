using VirtualPaper.Common;

namespace VirtualPaper.DraftPanel.Model.NavParam {
    public record ToDraftConfig(string ProjName, ProjectType ProjType);
    public record ToWorkSpace(string[] FilePaths);
}
