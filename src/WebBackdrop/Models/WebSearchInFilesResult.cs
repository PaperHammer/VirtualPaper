using System.Collections.ObjectModel;
using System.IO;

namespace Workloads.Creation.WebBackdrop.Models {
    public sealed class WebContentSearchFileResult {
        public string FilePath { get; init; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public string RelativePath { get; init; } = string.Empty;
        public ObservableCollection<WebContentSearchMatch> Matches { get; } = [];
        public int MatchCount => Matches.Count;
    }

    public sealed class WebContentSearchMatch {
        public string FilePath { get; init; } = string.Empty;
        public int LineNumber { get; init; }
        public int ColumnNumber { get; init; }
        public string PreviewText { get; init; } = string.Empty;
        public string LineLabel => LineNumber.ToString();
    }

    public sealed class WebQuickOpenItem {
        public string FilePath { get; init; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public string RelativePath { get; init; } = string.Empty;
    }
}
