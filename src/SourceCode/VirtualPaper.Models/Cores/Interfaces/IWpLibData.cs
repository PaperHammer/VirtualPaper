namespace VirtualPaper.Models.Cores.Interfaces {
    public interface IWpLibData {
        int Idx { get; set; }
        IWpMetadata Data { get; set; }

        bool IsAvailable();
    }
}
