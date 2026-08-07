using System;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores;
using VirtualPaper.PlayerWeb.Core.Interfaces;
using VirtualPaper.PlayerWeb.Core.WebView.Pages;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Windows {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PreviewWithWeb : ArcWindow, IApplyService {
        public event EventHandler<ApplyEventArgs>? Applied;

        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => _windowKey;

        public PreviewWithWeb(string jsonString, bool enableHmr = false) {
            _startArgs = JsonSerializer.Deserialize<StartArgsWeb>(jsonString);
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCore, _startArgs.FilePath + _startArgs.RuntimeType);
            _enableHmr = enableHmr;
            this.InitializeComponent();
            InitializeWindow();

            AfterFeReady();
        }

        private async void NaviContent_Loaded(object sender, RoutedEventArgs e) {
            try {
                var payload = new FrameworkPayload() {
                    [NaviPayloadKey.StartArgs.ToString()] = _startArgs,
                    [NaviPayloadKey.IWpBasicData.ToString()] = _wpBasicData,
                    [NaviPayloadKey.ArcWindow.ToString()] = this,
                    [NaviPayloadKey.ApplyService.ToString()] = this,
                };
                NaviContent.Navigate(typeof(PageWithPlaying), payload);

                // Wire up HMR for WebBackdrop editor debug sessions.
                // Set EnableHmr immediately so NavigationCompleted calls
                // InitPreviewManager() before the ResourceLoad script runs.
                if (_enableHmr
                    && NaviContent.PageMap.TryGetValue(typeof(PageWithPlaying), out var page)
                    && page is PageWithPlaying playingPage) {
                    playingPage.EnableHmr = true;
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PageWithPlaying>().Error(ex);
            }
        }
        
        private async void AfterFeReady() {
            _wpBasicData ??= await JsonSaver.LoadAsync<WpBasicData>(_startArgs.WpBasicDataFilePath, WpBasicDataContext.Default);
            string windowTitle = !string.IsNullOrEmpty(_wpBasicData.Title) ? $"{_wpBasicData.Title} (Preview)" :
                (!string.IsNullOrEmpty(_startArgs.FilePath) ? $"{Path.GetFileName(_startArgs.FilePath)} (Preview)" : "Virtual Paper PlayerWeb (Preview)");
            this.Title = this.MainHost.Title = windowTitle;
        }

        public void OnApply(ApplyEventArgs args) {
            Applied?.Invoke(this, args);
        }

        /// <summary>
        /// File-type-aware hot reload.  <paramref name="relativePath"/> is
        /// relative to the project root (e.g. "css/style.css").
        /// </summary>
        public void OnFileChanged(string relativePath) {
            if (NaviContent.PageMap.TryGetValue(typeof(PageWithPlaying), out var page)
                && page is PageWithPlaying playingPage) {
                playingPage.OnFileChanged(relativePath);
            }
        }

        private readonly StartArgsWeb? _startArgs;
        private readonly ArcWindowManagerKey _windowKey;
        private readonly bool _enableHmr;
        private WpBasicData _wpBasicData;
    }
}
