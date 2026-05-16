using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Services {
    public class WindowService : IWindowService {
        public WindowService(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public void Show<TWindow>(object? parameter = null) where TWindow : Window {
            var windowType = typeof(TWindow);

            // 若已存在实例，则激活
            if (_openWindows.TryGetValue(windowType, out var existingWindow)) {
                if (existingWindow.WindowState == WindowState.Minimized)
                    existingWindow.WindowState = WindowState.Normal;
                existingWindow.Activate();
                return;
            }

            var window = _serviceProvider.GetRequiredService<TWindow>();

            // 尝试注入参数
            InjectParameter(window, parameter);

            // 监听关闭事件自动清理
            window.Closed += (_, _) => _openWindows.Remove(windowType);

            _openWindows[windowType] = window;
            window.Show();
            window.Activate();
        }

        public async Task<bool?> ShowDialogAsync<TWindow>(object? parameter = null) where TWindow : Window {
            var window = _serviceProvider.GetRequiredService<TWindow>();

            InjectParameter(window, parameter);
            return await Task.FromResult(window.ShowDialog());
        }

        public bool TryGet<TWindow>(out TWindow? window) where TWindow : Window {
            if (_openWindows.TryGetValue(typeof(TWindow), out var existing)) {
                window = existing as TWindow;
                return true;
            }

            window = null;
            return false;
        }

        private void InjectParameter(Window window, object? parameter) {
            if (parameter == null)
                return;

            if (window.DataContext is IWindowParameterReceiver receiver) {
                receiver.ReceiveParameter(parameter);
            }
        }

        private readonly IServiceProvider _serviceProvider;

        // 当前打开的窗口引用
        private readonly Dictionary<Type, Window> _openWindows = [];
    }

    /// <summary>
    /// 若 ViewModel 实现此接口，则可接收打开窗口时传入的参数。
    /// </summary>
    public interface IWindowParameterReceiver {
        void ReceiveParameter(object? parameter);
    }
}
