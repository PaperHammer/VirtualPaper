using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.IPC.Interfaces;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.PlayerWeb.Core;
using VirtualPaper.PlayerWeb.Core.WebView.Pages;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : ArcWindow, IIpcObserver {
        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => _windowKey;
        public StartArgsWeb Args => _startArgs;

        public MainWindow(StartArgsWeb startArgs) {
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCore, Args.FilePath + Args.RuntimeType);
            _startArgs = startArgs;
            this.InitializeComponent();
            base.InitializeWindow();

            ContentHost.Visibility = Visibility.Collapsed;
            _ = StdInListener();
        }

        private void ArcWindow_Closed(object sender, WindowEventArgs args) {
            PreClose();
        }

        private void NaviContent_Loaded(object sender, RoutedEventArgs e) {
            try {
                var payload = new NavigationPayload() {
                    [NaviPayLoadKey.StartArgs.ToString()] = _startArgs,
                    [NaviPayLoadKey.IIpcObserver.ToString()] = this,
                };
                NaviContent.Navigate(typeof(PageWithPlaying), payload);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainWindow>().Error(ex);
            }
        }

        #region ipcobserver
        public void Register(object subscriber) {
            if (subscriber == null) return;

            var itfs = subscriber.GetType().GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIpcSubscribe<>));

            foreach (var itf in itfs) {
                var messageType = itf.GetGenericArguments()[0];

                if (_handlers.ContainsKey(messageType)) {
                    continue;
                }

                // 通过表达式树编译，消除反射产生的 object[] 分配
                var handler = BuildHandler(subscriber, itf, messageType);
                _handlers[messageType] = handler;

                if (!_subscriberIndex.TryGetValue(subscriber, out var list)) {
                    _subscriberIndex[subscriber] = list = [];
                }
                list.Add(messageType);
            }
        }

        public void Unregister(object subscriber) {
            if (subscriber != null && _subscriberIndex.Remove(subscriber, out var types)) {
                foreach (var type in types) _handlers.Remove(type);
            }
        }

        public async ValueTask Dispatch(IpcMessage message) {
            if (message != null && _handlers.TryGetValue(message.GetType(), out var handler)) {
                if (message is VirtualPaperCloseCmd) {
                    this.Close();
                    return;
                }

                await handler(message);
            }
        }

        private static Func<IpcMessage, ValueTask> BuildHandler(object instance, Type itf, Type msgType) {
            var method = itf.GetMethod("OnIpcAsync")!;
            var msgParam = Expression.Parameter(typeof(IpcMessage), "msg");

            // 生成: ((IIpcSubscribe<T>)instance).OnIpcAsync((T)msg)
            var call = Expression.Call(
                Expression.Constant(instance),
                method,
                Expression.Convert(msgParam, msgType)
            );

            return Expression.Lambda<Func<IpcMessage, ValueTask>>(call, msgParam).Compile();
        }
        #endregion

        private async Task StdInListener() {
            try {
                await Task.Run(async () => {
                    while (!_ctsConsoleIn.IsCancellationRequested) {
                        var msg = await Console.In.ReadLineAsync(_ctsConsoleIn.Token);
                        if (string.IsNullOrEmpty(msg)) {
                            App.WriteToParent(new VirtualPaperMessageConsole {
                                MsgType = ConsoleMessageType.Log,
                                Message = "Ipc stdin none, closing"
                            });
                            //When the redirected stream is closed, a null line is sent to the event handler. 
#if !DEBUG
                            break;
#endif
                        }
                        else {
                            HandleIpcMessage(msg);
                        }
                    }
                });
            }
            catch (Exception e) {
                App.WriteToParent(new VirtualPaperMessageConsole {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Ipc stdin Error: {e.Message}"
                });
            }
        }

        private async void HandleIpcMessage(string message) {
            try {
                var obj = JsonSerializer.Deserialize(message, IpcMessageContext.Default.IpcMessage);
                await Dispatch(obj);
            }
            catch (Exception e) {
                ArcLog.GetLogger<MainWindow>().Error(e);
                App.WriteToParent(new VirtualPaperMessageConsole {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Ipc action Error: {e.Message}"
                });
            }
        }

        private void PreClose() {
            this.Hide();

            _ctsConsoleIn?.Cancel();
            CrossThreadInvoker.InvokeOnUIThread(() => {
                App.AppInstance.Exit();
            });
        }

        private readonly StartArgsWeb _startArgs;
        private readonly ArcWindowManagerKey _windowKey;
        private readonly CancellationTokenSource _ctsConsoleIn = new();
        // messageType -> handler
        private readonly Dictionary<Type, Func<IpcMessage, ValueTask>> _handlers = [];
        // subscriber -> messageTypes（用于 Unregister）
        private readonly Dictionary<object, List<Type>> _subscriberIndex = [];
    }
}
