using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Grpc.Client.Interfaces
{
    public interface IUserSettingsClient
    {
        ISettings Settings { get; }
        List<IApplicationRules> AppRules { get; }
        Task SaveAsync<T>();
        void Save<T>();
        Task LoadAsync<T>();
        void Load<T>();
    }
}
