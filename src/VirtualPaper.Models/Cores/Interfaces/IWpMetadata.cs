namespace VirtualPaper.Models.Cores.Interfaces {
    public interface IWpMetadata {
        IWpBasicData BasicData { get; set; }
        IWpRuntimeData RuntimeData { get; set; }
        IWpPlayerData GetPlayerData();
        void Read(string folderPath);
        Task MoveToAsync(string targetFolderPath);
        void Save();
        bool IsAvailable();
    }
}
