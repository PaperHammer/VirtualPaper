using System.Diagnostics;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;

namespace VirtualPaper.Cores
{
    /// <summary>
    /// 播放时的壁纸对象
    /// </summary>
    public interface IWallpaper
    {
        /// <summary>
        /// Get process information.
        /// </summary>
        /// <returns>null if not a program wallpaper.</returns>
        Process Proc { get; }

        /// <summary>
        /// Get window handle.
        /// </summary>
        /// <returns></returns>
        IntPtr Handle { get; }

        /// <summary>
        /// Get handle to input window.
        /// </summary>
        /// <returns></returns>
        IntPtr InputHandle { get; }

        /// <summary>
        /// 壁纸元数据
        /// </summary>
        /// <returns></returns>
        IMetaData MetaData { get; }

        /// <summary>
        /// 获取当前正在运行壁纸的显示设备
        /// </summary>
        /// <returns></returns>
        IMonitor Monitor { get; set; }

        /// <summary>
        /// Wallpaper exit event fired
        /// </summary>
        bool IsExited { get; }

        /// <summary>
        /// 壁纸加载完成状态
        /// </summary>
        /// <returns></returns>
        bool IsLoaded { get; }

        Task<bool> ShowAsync(CancellationToken cancellationToken = default);

        void Pause();

        void Play();

        void Stop();

        void Close();

        void ClosePreview();

        /// <summary>
        /// 发送 ipc 消息到壁纸窗口
        /// </summary>
        /// <param name="ipcMsg"></param>
        void SendMessage(IpcMessage ipcMsg);

        /// <summary>
        /// Mute/disable audio track.
        /// </summary>
        /// <param name="mute">true: mute audio</param>
        void SetMute(bool mute);

        /// <summary>
        /// Sets wallpaper position in timeline. 设置墙纸在时间轴中的位置<br>
        /// Only value 0 works for non-video _wallpapers. 只有值 0 适用于非视频</br>
        /// </summary>
        /// <param name="pos">Range 0 - 100</param>
        void SetPlaybackPos(float pos, PlaybackPosType type);

        void Modify(string controlName, string propertyName, string value);

        void Update(IMetaData metaData);
        /// <summary>
        /// 截图保存当前壁纸（.jpg）
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        Task ScreenCapture(string filePath);

    }

    public enum PlaybackPosType
    {
        absolutePercent,
        relativePercent
    }

    public class WindowInitializedArgs : EventArgs
    {
        /// <summary>
        /// True if wallpaper window is ready.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Errors if any.
        /// Null if no Error.
        /// </summary>
        public Exception? Error { get; set; }

        /// <summary>
        /// Custom message.
        /// Null if no message.
        /// </summary>
        public string Msg { get; set; } = string.Empty;
    }
}
