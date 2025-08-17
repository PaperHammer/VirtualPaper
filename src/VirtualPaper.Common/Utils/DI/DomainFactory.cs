using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace VirtualPaper.Common.Utils.DI {
    public abstract class DomainFactory<TTool> where TTool : class, IDisposable {
        public static TTool GetTool(object domainKey) {
            var provider = _domainProviders.GetOrAdd(domainKey, _ => {
                var services = new ServiceCollection();
                ConfigureToolServices(services);
                return services.BuildServiceProvider();
            });

            return provider.GetRequiredService<TTool>();
        }

        public static void ConfigureToolServices(IServiceCollection services) {
            services.AddSingleton<TTool>();
        }

        public static void ReleaseDomain(object domainKey) {
            if (_domainProviders.TryRemove(domainKey, out var provider)) {
                (provider as IDisposable)?.Dispose();
            }
        }

        private static readonly ConcurrentDictionary<object, IServiceProvider> _domainProviders = new();
    }
}
