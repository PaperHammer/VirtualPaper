using VirtualPaper.Common;
using VirtualPaper.Cores;
using VirtualPaper.Cores.Players.Web;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Factories
{
    public class WallpaperFactory(IWallpaperConfigFolderFactory wpConfigFolderFactory) : IWallpaperFactory
    {
        public IWallpaper CreateWallpaper(IMetaData metaData, IMonitor monitor, IUserSettingsService userSettings, bool isPreview = false)
        {
            switch (metaData.Type)
            {
                case WallpaperType.picture:
                case WallpaperType.gif:
                case WallpaperType.video:
                    {
                        return new Webviewer(
                            metaData,
                            monitor,
                            isPreview);
                    }

                //case WallpaperType.web:
                //case WallpaperType.webaudio:
                //case WallpaperType.video:
                //    switch (_userSetting.Settings.VideoPlayer)
                //    {
                //        case VirtualPaperMediaPlayer.wmf:
                //            return new VideoWmfPlayer(metaData.FilePath, metaData,
                //                monitor, 0, _userSetting.Settings.WallpaperScaling);
                //        case VirtualPaperMediaPlayer.mpv:
                //            return new VideoMpvPlayer(metaData.FilePath,
                //                metaData,
                //                monitor,
                //                _wpCustomizeFolderFactory.CreateWpConfigFolder(metaData, monitor, _userSetting.Settings.WallpaperArrangement, _userSetting),
                //                _userSetting.Settings.WallpaperScaling,
                //                _userSetting.Settings.IsStartHwAccel,
                //                isPreview);
                //        case VirtualPaperMediaPlayer.vlc:
                //            return new VideoVlcPlayer(metaData.FilePath,
                //                metaData,
                //                monitor,
                //                _userSetting.Settings.WallpaperScaling,
                //                _userSetting.Settings.IsStartHwAccel);
                //    }
                //    break;
                //case WallpaperType.gif:
                //    switch (_userSetting.Settings.GifPlayer)
                //    {
                //        case VirtualPaperGifPlayer.mpv:
                //            return new VideoMpvPlayer(metaData.FilePath,
                //                           metaData,
                //                           monitor,
                //                           _wpCustomizeFolderFactory.CreateWpConfigFolder(metaData, monitor, _userSetting.Settings.WallpaperArrangement, _userSetting),
                //                           _userSetting.Settings.WallpaperScaling,
                //                           _userSetting.Settings.IsStartHwAccel,
                //                           isPreview);
                //    }
                //    break;
                //case WallpaperType.picture:
                //    switch (_userSetting.Settings.PicturePlayer)
                //    {
                //        case VirtualPaperPicturePlayer.winApi:
                //            return new PictureWinApi(metaData.FilePath, metaData, monitor, _userSetting.Settings.WallpaperArrangement, _userSetting.Settings.WallpaperScaling);
                //        case VirtualPaperPicturePlayer.mpv:
                //            return new VideoMpvPlayer(metaData.FilePath,
                //                              metaData,
                //                              monitor,
                //                              _wpCustomizeFolderFactory.CreateWpConfigFolder(metaData, monitor, _userSetting.Settings.WallpaperArrangement, _userSetting),
                //                              _userSetting.Settings.WallpaperScaling,
                //                              _userSetting.Settings.IsStartHwAccel,
                //                              isPreview);
                //        case VirtualPaperPicturePlayer.wmf:
                //            return new VideoWmfPlayer(metaData.FilePath, metaData, monitor, 0, _userSetting.Settings.WallpaperScaling);
                //    }
                //    break;
                //case WallpaperType.app:
                //case WallpaperType.bizhawk:
                //case WallpaperType.unity:
                //case WallpaperType.unityaudio:
                //case WallpaperType.godot:
                //    if (Constants.ApplicationType.IsMSIX)
                //    {
                //        throw new MsixNotAllowedException("Program wallpaper on MSIX package not allowed.");
                //    }
                //    else
                //    {
                //        return new ExtPrograms(metaData.FilePath, metaData, monitor,
                //          _userSetting.Settings.WallpaperWaitTime);
                //    }
                //case WallpaperType.videostream:
                //    if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "mpv", "youtube-dl.exe")))
                //    {
                //        return new VideoMpvPlayer(metaData.FilePath,
                //            metaData,
                //            monitor,
                //            _wpCustomizeFolderFactory.CreateWpConfigFolder(metaData, monitor, _userSetting.Settings.WallpaperArrangement, _userSetting),
                //            _userSetting.Settings.WallpaperScaling, _userSetting.Settings.IsStartHwAccel,
                //            isPreview, _userSetting.Settings.StreamQuality);
                //    }
                //    else
                //    {
                //        return new WebCefSharpProcess(metaData.FilePath,
                //                metaData,
                //                monitor,
                //                _wpCustomizeFolderFactory.CreateWpConfigFolder(metaData, monitor, _userSetting.Settings.WallpaperArrangement, _userSetting),
                //                _userSetting.Settings.WebDebugPort,
                //                _userSetting.Settings.IsCefDiskCache,
                //                _userSetting.Settings.AudioVolumeGlobal);
                //    }
            }
            throw new PluginNotFoundException("Wallpaper player not found.");
        }

        #region exceptions
        public class MsixNotAllowedException : Exception
        {
            public MsixNotAllowedException()
            {
            }

            public MsixNotAllowedException(string message)
                : base(message)
            {
            }

            public MsixNotAllowedException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        public class DepreciatedException : Exception
        {
            public DepreciatedException()
            {
            }

            public DepreciatedException(string message)
                : base(message)
            {
            }

            public DepreciatedException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        public class PluginNotFoundException : Exception
        {
            public PluginNotFoundException()
            {
            }

            public PluginNotFoundException(string message)
                : base(message)
            {
            }

            public PluginNotFoundException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }
        #endregion
    }
}
