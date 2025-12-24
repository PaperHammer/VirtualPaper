using System;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.PlayerWeb.Core.WebView.Components;
using VirtualPaper.PlayerWeb.Core.WebView.Pages;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Windows {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AdjustConfig : ArcWindow {
        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => _windowKey;

        public AdjustConfig(StartArgsWeb startArgs) {
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCoreAdjustTool, startArgs.FilePath + startArgs.RuntimeType);
            this.InitializeComponent();
        }

        private void NaviContent_Loaded(object sender, RoutedEventArgs e) {
            try {
                //var previewWindow = ArcWindowManager.GetArcWindow(_windowKey);
                // todo
                NaviContent.Navigate(typeof(PageOnlyDataConfig));
            }
            catch (Exception ex) {
                ArcLog.GetLogger<AdjustConfig>().Error(ex);
            }
        }

        private readonly ArcWindowManagerKey _windowKey;
    }
}
