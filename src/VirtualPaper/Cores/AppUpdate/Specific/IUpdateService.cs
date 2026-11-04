using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Cores.AppUpdate.Specific {
    public interface IUpdateService {
        Task<bool> DownloadUpdateAsync(ReleaseInfo info, IProgress<DownloadProgress> progress, CancellationToken token);
        Task<bool> VerifyUpdateAsync(ReleaseInfo info, CancellationToken token);
    }

    public static class UpdateServiceFactory {
        public static IUpdateService Resolve(IServiceProvider sp, ReleaseInfo info) {
            return info.IsPluginsUpdate
                ? sp.GetRequiredService<IPluginsUpdateService>()
                : sp.GetRequiredService<IInstallerUpdateService>();
        }
    }
}
