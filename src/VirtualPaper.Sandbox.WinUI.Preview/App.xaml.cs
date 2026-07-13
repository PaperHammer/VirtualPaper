using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.Sandbox.WinUI.Preview {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application {
        private Window? _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
            AppServiceLocator.Services = ConfigureServices();
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            CrossThreadInvoker.Initialize(new UiSynchronizationContext());

            if (Constants.ApplicationType.IsMSIX) {
                await LanguageUtil.InitializeLocalizerForPackaged("zh-CN");
            }
            else {
                await LanguageUtil.InitializeLocalizerForUnpackaged("zh-CN");
            }

            _window = new MainWindow();
            _window.Activate();
        }

        private static ServiceProvider ConfigureServices() {
            return new ServiceCollection()
                .AddSingleton<IUserSettingsClient, StubUserSettingsClient>()
                .BuildServiceProvider();
        }

        private sealed class StubUserSettingsClient : IUserSettingsClient {
            public ISettings Settings { get; } = new Settings();
            public List<IApplicationRules> AppRules { get; } = [];
            public List<IWallpaperLayout> WallpaperLayouts { get; } = [];
            public List<IRecentUsed> RecentUseds { get; } = [];

            public void Dispose() { }
            public void Load<T>() { }
            public Task LoadAsync<T>() => Task.CompletedTask;
            public void Save<T>() { }
            public Task SaveAsync<T>() => Task.CompletedTask;
            public Task UpdateRecentUsedAsync(string filePath) => Task.CompletedTask;
            public Task UpdateRecetUsedAsync(string[] filePath) => Task.CompletedTask;
            public Task DeleteRecetUsedAsync(IRecentUsed item) => Task.CompletedTask;
        }
    }
}
