using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Server {
    public sealed partial class PreviewServer {
        public int Port { get; private set; }
        public bool IsRunning => _app != null;

        public async Task StartAsync(string projectRoot, PreviewServerOptions? options = null) {
            if (IsRunning)
                return;

            options ??= new PreviewServerOptions();
            _root = projectRoot;
            var builder = WebApplication.CreateBuilder();
            builder.WebHost
                .UseKestrel()
                .UseUrls($"http://{options.Host}:{options.Port}");

            _app = builder.Build();

            if (!string.IsNullOrWhiteSpace(options.AllowedOrigin)) {
                var allowedOrigin = options.AllowedOrigin;
                _app.Use(async (context, next) => {
                    context.Response.Headers["Access-Control-Allow-Origin"] = allowedOrigin;
                    await next(context);
                });
            }

            _app.UseStaticFiles(new StaticFileOptions {
                FileProvider = new PhysicalFileProvider(_root)
            });
            await _app.StartAsync();

            var address = _app.Urls.First();
            Port = new Uri(address).Port;
        }

        /// <summary>
        /// 获取预览URL
        /// </summary>
        public string GetUrl(string relativePath) {
            relativePath = relativePath.Replace("\\", "/");

            return $"http://127.0.0.1:{Port}/{relativePath}";
        }

        public async Task StopAsync() {
            // Atomically take ownership so concurrent calls don't
            // double-dispose the same WebApplication instance.
            var app = Interlocked.Exchange(ref _app, null);
            if (app == null)
                return;

            await app.StopAsync();
            await app.DisposeAsync();
        }

        private WebApplication? _app;
        private string _root = string.Empty;
    }
}
