using System.Windows;

namespace VirtualPaper.Services.Interfaces {
    public interface IWindowService {
        /// <summary>
        /// 打开一个窗口（非模态）
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        /// <param name="parameter">传递给 ViewModel 的初始化参数</param>
        void Show<TWindow>(object? parameter = null) where TWindow : Window;

        /// <summary>
        /// 打开一个模态窗口
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        /// <param name="parameter">传递给 ViewModel 的初始化参数</param>
        Task<bool?> ShowDialogAsync<TWindow>(object? parameter = null) where TWindow : Window;

        /// <summary>
        /// 尝试获取当前已打开的某类型窗口
        /// </summary>
        bool TryGet<TWindow>(out TWindow? window) where TWindow : Window;
    }
}
