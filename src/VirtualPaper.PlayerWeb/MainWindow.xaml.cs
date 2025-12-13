using Microsoft.UI.Xaml;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : ArcWindow {
        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => _windowKey;
        public StartArgsWeb Args => _startArgs;
        public ArcPageContext Context => this.ContentPage.Context;

        public MainWindow(StartArgsWeb startArgs) {
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCore, Args.FilePath + Args.RuntimeType);
            _startArgs = startArgs;
            this.InitializeComponent();
            base.InitializeWindow();

            ContentHost.Visibility = Visibility.Collapsed;
        }

        private readonly StartArgsWeb _startArgs;
        private readonly ArcWindowManagerKey _windowKey;
    }
}
