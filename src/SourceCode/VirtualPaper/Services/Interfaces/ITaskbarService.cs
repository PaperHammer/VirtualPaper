using System.Drawing;
using VirtualPaper.Common;

namespace VirtualPaper.Services.Interfaces
{
    /// <summary>
    /// 任务栏设置
    /// </summary>
    public interface ITaskbarService : IDisposable
    {
        bool IsRunning { get; }
        /// <summary>
        /// 检查与任务栏服务不兼容的程序，并返回可能包含相关详细信息的字符串
        /// </summary>
        /// <returns></returns>
        string CheckIncompatiblePrograms();
        /// <summary>
        /// 返回该文件关联的平均颜色
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        Color GetAverageColor(string filePath);
        /// <summary>
        /// 设置任务栏的主题强调色
        /// </summary>
        /// <param name="color"></param>
        void SetAccentColor(Color color);
        void Start(TaskbarTheme theme);
        void Stop();
    }
}
