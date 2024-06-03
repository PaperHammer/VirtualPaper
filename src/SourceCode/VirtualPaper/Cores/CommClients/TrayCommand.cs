using NLog;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using VirtualPaper.Cores.Desktop;
using VirtualPaper.Cores.ScreenSaver;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Cores.CommClients
{
    public class TrayCommand
    {
        public TrayCommand(
            IUserSettingsService userSettingsService,
            IWallpaperControl wpControl,
            IScrControl scrControl)
        {            
            _userSettingsService = userSettingsService;
            _wpControl = wpControl;
            _scrControl = scrControl;
        }

        public async void SendMsgToUI(string msg)
        {
            try
            {
                using (var client = new NamedPipeClientStream("localhost", "TRAY_CMD", PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.None))
                {
                    await client.ConnectAsync();

                    using (var writer = new StreamWriter(client))
                    {
                        writer.AutoFlush = true;
                        writer.WriteLine(msg);
                        client.WaitForPipeDrain();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[PipeClient] Exception: {ex.Message}");

                var wl = _userSettingsService.WallpaperLayouts;
                if (wl.Count > 0)
                {
                    var wpMetaData = _wpControl.GetWallpaper(wl[0].FolderPath);
                    MetaData metaData = new()
                    {
                        Type = wpMetaData.Type,
                        FilePath = wpMetaData.FilePath,
                    };
                    _scrControl.Start(metaData);
                }
            }
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private IUserSettingsService _userSettingsService;
        private IWallpaperControl _wpControl;
        private IScrControl _scrControl;
    }
}
