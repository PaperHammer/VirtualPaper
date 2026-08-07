using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

            // Inject HMR script into HTML responses if configured.
            if (!string.IsNullOrEmpty(options.InjectionScript)) {
                var injection = options.InjectionScript;
                var root = _root;

                // Serve the injection script as a standalone file so browser
                // DevTools shows a proper file name in error stacks.
                _app.MapGet("/__hmr.js", async ctx => {
                    ctx.Response.ContentType = "application/javascript; charset=utf-8";
                    await ctx.Response.WriteAsync(injection);
                });

                _app.UseWhen(
                    ctx => IsHtmlPath(ctx.Request.Path),
                    branch => branch.Run(async ctx => {
                        var relativePath = ctx.Request.Path.Value!.TrimStart('/');
                        if (relativePath.Length == 0 || relativePath.EndsWith("/"))
                            relativePath = Path.Combine(relativePath, "index.html").Replace('\\', '/');
                        var filePath = Path.Combine(root, relativePath);
                        if (!File.Exists(filePath)) { ctx.Response.StatusCode = 404; return; }

                        var html = await File.ReadAllTextAsync(filePath);
                        var tag = "<script src=\"/__hmr.js\"></script>";
                        if (html.Contains("</body>"))
                            html = html.Replace("</body>", tag + "</body>");
                        else if (html.Contains("</head>"))
                            html = html.Replace("</head>", tag + "</head>");
                        else
                            html += tag;

                        ctx.Response.ContentType = "text/html; charset=utf-8";
                        await ctx.Response.WriteAsync(html);
                    }));
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

        private static bool IsHtmlPath(string path) {
            var ext = Path.GetExtension(path);
            if (ext.Equals(".html", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".htm", StringComparison.OrdinalIgnoreCase))
                return true;
            // Default document: / or /subdir/ → index.html
            return path.EndsWith("/");
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
