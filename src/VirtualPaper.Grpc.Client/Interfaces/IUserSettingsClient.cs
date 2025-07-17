using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface IUserSettingsClient {
        ISettings Settings { get; }
        List<IApplicationRules> AppRules { get; }
        List<IWallpaperLayout> WallpaperLayouts { get; }
        List<IRecentUsed> RecentUseds { get; }
        Task SaveAsync<T>();
        void Save<T>();
        Task LoadAsync<T>();
        void Load<T>();
        Task UpdateRecetUsedAsync(string filePath);
        Task DeleteRecetUsedAsync(IRecentUsed item);
    }
}
