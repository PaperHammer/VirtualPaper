using System.Windows;
using System.Windows.Interop;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Services {
    public class WindowService : IWindowService {
        public WindowService(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public void Show<TWindow>(object? parameter = null, bool bringToFront = false) where TWindow : class {
            if (_openWindows.TryGetValue(typeof(TWindow), out var existing)) {
                if (bringToFront) {
                    BringToFront(existing);
                } else {
                    existing.Activate();
                }
                return;
            }

            var window = CreateWindow<TWindow>();
            InjectParameter(window, parameter);

            window.Closed += (_, _) => _openWindows.Remove(typeof(TWindow));
            _openWindows[typeof(TWindow)] = window;
            window.Show();
            if (bringToFront) {
                BringToFront(window);
            } else {
                window.Activate();
            }
        }

        private static void BringToFront(Window window) {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            Native.SetForegroundWindow(hwnd);
        }

        public Task<bool?> ShowDialogAsync<TWindow>(object? parameter = null) where TWindow : class {
            var tcs = new TaskCompletionSource<bool?>();

            var window = CreateWindow<TWindow>();
            InjectParameter(window, parameter);

            window.Closed += (_, _) =>
            {
                _openWindows.Remove(typeof(TWindow));
                tcs.TrySetResult(window.DialogResult);
            };

            _openWindows[typeof(TWindow)] = window;
            window.ShowDialog();

            return tcs.Task;
        }

        public bool TryGet<TWindow>(out TWindow? window) where TWindow : class {
            if (_openWindows.TryGetValue(typeof(TWindow), out var w) && w is TWindow typed) {
                window = typed;
                return true;
            }
            window = null;
            return false;
        }

        public void Close<TWindow>() where TWindow : class {
            if (_openWindows.TryGetValue(typeof(TWindow), out var window)) {
                window.Close();
                // Closed 事件会自动从字典中移除
            }
        }

        private Window CreateWindow<TWindow>() where TWindow : class {
            var window = _serviceProvider.GetService(typeof(TWindow)) as Window
                ?? throw new InvalidOperationException(
                    $"ProjType {typeof(TWindow).Name} is not registered or is not a Window.");
            return window;
        }

        private static void InjectParameter(Window window, object? parameter) {
            if (parameter != null && window.DataContext is IWindowParameterReceiver receiver) {
                receiver.ReceiveParameter(parameter);
            }
        }

        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, Window> _openWindows = [];
    }

    /// <summary>
    /// 若 ViewModel 实现此接口，则可接收打开窗口时传入的参数。
    /// </summary>
    public interface IWindowParameterReceiver {
        void ReceiveParameter(object? parameter);
    }
}
