namespace VirtualPaper.RepoPanel.Utils {
    public interface IFilterable {
        FilterKey FilterKeyword { get; set; }
        void ApplyFilter(string keyword);
    }

    public enum FilterKey {
        LibraryTitle,
    }
}
