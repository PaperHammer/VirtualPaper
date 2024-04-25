using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Services.Interfaces
{
    /// <summary>
    /// 用户设置
    /// </summary>
    public interface IUserSettingsService
    {
        ISettings Settings { get; }
        List<IApplicationRules> AppRules { get; }
        List<IWallpaperLayout> WallpaperLayout { get; }
        void Save<T>();
        void Load<T>();
    }
}
