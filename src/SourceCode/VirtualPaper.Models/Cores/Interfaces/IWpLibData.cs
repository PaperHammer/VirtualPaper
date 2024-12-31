namespace VirtualPaper.Models.Cores.Interfaces {
    public interface IWpLibData {
        int Idx { get; set; }
        IWpBasicData BasicData { get; set; }

        bool IsAvailable();
    }
}
